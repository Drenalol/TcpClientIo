using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace Drenalol.TcpClientIo.Emulator;

/// <summary>
/// Echo TCP server: sends back exactly what it received. Drops a connection after <see cref="IdleTimeout"/>
/// of silence and stops accepting when the token is cancelled.
/// </summary>
public sealed class ListenerEmulator(CancellationToken token, ListenerEmulatorConfig config)
{
    /// <summary>
    /// Idle time after which the emulator closes an accepted connection. Tests rely on this value
    /// being longer than their own cancellation timeouts.
    /// </summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(5);

    private readonly TcpListener _listener = TcpListener.Create(config.Port);

    /// <summary>
    /// The port the emulator is actually listening on (useful when <see cref="ListenerEmulatorConfig.Port"/> is 0).
    /// </summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public ListenerEmulator StartListening()
    {
        _listener.Start();
        _ = GetConnection();
        return this;
    }

    public static ListenerEmulator Create(CancellationToken token, ListenerEmulatorConfig args) => new ListenerEmulator(token, args).StartListening();

    private async Task GetConnection()
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleConnection(tcpClient), CancellationToken.None);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // listener stopped, expected on shutdown
        }
    }

    private async Task HandleConnection(TcpClient tcpClient)
    {
        var sw = Stopwatch.StartNew();
        var reader = PipeReader.Create(tcpClient.GetStream(), new StreamPipeReaderOptions(config.ReaderMemoryPool ? MemoryPool<byte>.Shared : null, config.ReaderBufferSize, config.ReaderMinimumReadSize));
        var writer = PipeWriter.Create(tcpClient.GetStream(), new StreamPipeWriterOptions(config.WriterMemoryPool ? MemoryPool<byte>.Shared : null, config.WriterBufferSize));

        try
        {
            while (tcpClient.Connected && !token.IsCancellationRequested && sw.Elapsed < IdleTimeout)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    var readResult = await reader.ReadAsync(token);

                    token.ThrowIfCancellationRequested();

                    if (readResult.Buffer.IsEmpty)
                        continue;

                    foreach (var segment in readResult.Buffer)
                        writer.Write(segment.Span);
                    await writer.FlushAsync(token);

                    reader.AdvanceTo(readResult.Buffer.End);
                    sw.Restart();
                }
                catch (OperationCanceledException)
                {
                    _listener.Stop();
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
            await writer.CompleteAsync();
            tcpClient.Close();
        }
    }
}
