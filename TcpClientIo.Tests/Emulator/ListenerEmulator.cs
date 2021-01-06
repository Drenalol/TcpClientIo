using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.TcpClientIo.Emulator
{
    
    public class ListenerEmulator
    {
        private readonly ListenerEmulatorConfig _config;
        private readonly CancellationToken _token;
        private readonly TcpListener _listener;

        private ListenerEmulator(CancellationToken token, ListenerEmulatorConfig config)
        {
            _token = token;
            _config = config;
            _listener = TcpListener.Create(_config.Port);
            _listener.Start();
            _ = GetConnection();
        }

        public static ListenerEmulator Create(CancellationToken token, ListenerEmulatorConfig args)
        {
            return new ListenerEmulator(token, args);
        }

        private async Task GetConnection()
        {
            while (!_token.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleConnection(tcpClient), _token);
            }
        }

        private async Task HandleConnection(TcpClient tcpClient)
        {
            var sw = Stopwatch.StartNew();
            var reader = PipeReader.Create(tcpClient.GetStream(), new StreamPipeReaderOptions(_config.ReaderMemoryPool ? MemoryPool<byte>.Shared : null, _config.ReaderBufferSize, _config.ReaderMinimumReadSize));
            var writer = PipeWriter.Create(tcpClient.GetStream(), new StreamPipeWriterOptions(_config.WriterMemoryPool ? MemoryPool<byte>.Shared : null, _config.WriterBufferSize));
            
            while (tcpClient.Connected && !_token.IsCancellationRequested && sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                try
                {
                    _token.ThrowIfCancellationRequested();
                    
                    var readResult = await reader.ReadAsync(_token);

                    _token.ThrowIfCancellationRequested();
                    
                    if (readResult.Buffer.IsEmpty)
                        continue;

                    if (readResult.Buffer.IsSingleSegment)
                        await writer.WriteAsync(readResult.Buffer.First, _token);
                    else
                        foreach (var readOnlyMemory in readResult.Buffer)
                            await writer.WriteAsync(readOnlyMemory, _token);

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

            await reader.CompleteAsync();
            await writer.CompleteAsync();
            tcpClient.Close();
        }
    }
}