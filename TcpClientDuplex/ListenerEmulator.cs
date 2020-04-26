using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpClientDuplex
{
    public class ListenerEmulator
    {
        private readonly CancellationToken _token;
        private readonly TcpListener _listener;
        
        private ListenerEmulator(int port, CancellationToken token)
        {
            _token = token;
            _listener = TcpListener.Create(port);
            _listener.Start();
            Task.Factory.StartNew(GetConnection, TaskCreationOptions.LongRunning);
        }

        public static ListenerEmulator Create(int port, CancellationToken token)
        {
            var listenerEmulator = new ListenerEmulator(port, token);
            return listenerEmulator;
        }

        private async Task GetConnection()
        {
            while (!_token.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleConnection(tcpClient), _token);
            }
        }

        private static async Task HandleConnection(TcpClient tcpClient)
        {
            var stream = tcpClient.GetStream();
            var reader = PipeReader.Create(stream);
            while (tcpClient.Connected)
            {
                var readResult = await reader.ReadAsync();
                if (readResult.Buffer.IsSingleSegment)
                    await stream.WriteAsync(readResult.Buffer.First);
                else
                    foreach (var readOnlyMemory in readResult.Buffer)
                        await stream.WriteAsync(readOnlyMemory);
                reader.AdvanceTo(readResult.Buffer.End);
            }
            await reader.CompleteAsync();
        }
    }
}