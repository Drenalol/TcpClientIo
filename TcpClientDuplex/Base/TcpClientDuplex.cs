using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpClientDuplex.Base
{
    public class TcpClientDuplex : IDuplexPipe
    {
        private readonly CancellationToken _cancellationToken;
        private readonly BlockingCollection<byte[]> _toWrite;
        private readonly ConcurrentDictionary<uint, TcpPackage> _output;
        private PipeReader Input { get; set; }
        PipeReader IDuplexPipe.Input => Input;
        private PipeWriter Output { get; set; }
        PipeWriter IDuplexPipe.Output => Output;
        public int Queue => _output.Count;
        public int ToWrite => _toWrite.Count;

        public TcpClientDuplex(IPAddress address, int port, Func<PipeReader, CancellationToken, TcpPackage> readFactory, CancellationToken cancellationToken = default) : this(address, port, cancellationToken)
        {
            Task.Factory.StartNew(TcpWrite, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => TcpRead(readFactory), TaskCreationOptions.LongRunning);
        }

        public TcpClientDuplex(IPAddress address, int port, Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory, CancellationToken cancellationToken = default) : this(address, port, cancellationToken)
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => TcpReadAsync(readFactory), TaskCreationOptions.LongRunning);
        }

        private TcpClientDuplex(IPAddress address, int port, CancellationToken cancellationToken = default)
        {
            _cancellationToken = cancellationToken;
            _toWrite = new BlockingCollection<byte[]>();
            _output = new ConcurrentDictionary<uint, TcpPackage>();
            CreateTcpClient(address, port);
        }

        private void CreateTcpClient(IPAddress address, in int port)
        {
            var tcpClient = new TcpClient();
            tcpClient.Connect(address, port);

            if (!tcpClient.Connected)
                throw new SocketException();

            Input = PipeReader.Create(tcpClient.GetStream());
            Output = PipeWriter.Create(tcpClient.GetStream());
        }

        public bool TrySend(byte[] data) => _toWrite.TryAdd(data, 1000, _cancellationToken);

        public TcpPackage Receive(uint packageId, int timeout = 300)
        {
            var sw = Stopwatch.StartNew();
            while (!_cancellationToken.IsCancellationRequested && sw.Elapsed < TimeSpan.FromSeconds(timeout))
            {
                if (_output.TryRemove(packageId, out var package))
                    return package;
            }
            
            throw new TimeoutException();
        }

        private async Task TcpWriteAsync()
        {
            foreach (var byteArray in _toWrite.GetConsumingEnumerable())
            {
                try
                {
                    var length = byteArray.Length;
                    byteArray.CopyTo(Output.GetMemory(length));
                    Output.Advance(length);
                    await Output.FlushAsync(_cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _toWrite.CompleteAdding();
                    _toWrite.Dispose();
                    await Output.CompleteAsync();
                }
            }
        }

        private async Task TcpReadAsync(Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory)
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var attempt = 0;
                    var package = await readFactory(Input, _cancellationToken);
                    while (attempt <= 10 && !_cancellationToken.IsCancellationRequested)
                    {
                        if (attempt >= 10)
                            throw new AggregateException("attempt exceeded");
                        
                        if (_output.TryAdd(package.PackageId, package))
                            break;
                        
                        attempt++;
                    }
                }
                catch (OperationCanceledException)
                {
                    await Input.CompleteAsync();
                }
            }
        }

        private void TcpWrite() => throw new NotImplementedException();
        private void TcpRead(Func<PipeReader, CancellationToken, TcpPackage> readFactory) => throw new NotImplementedException();
    }
}