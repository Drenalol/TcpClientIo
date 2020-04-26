using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpClientDuplex
{
    public class TcpClientDuplex<TKey, TValue> : IDuplexPipe
    {
        private readonly CancellationToken _cancellationToken;
        private readonly BlockingCollection<byte[]> _toWrite;
        private readonly ImmutableDictionary<TKey, ITcpPackage<TKey, TValue>> _output;
        private PipeReader Input { get; set; }
        PipeReader IDuplexPipe.Input => Input;
        private PipeWriter Output { get; set; }
        PipeWriter IDuplexPipe.Output => Output;

        public TcpClientDuplex(IPAddress address, int port, Func<PipeReader, CancellationToken, ITcpPackage<TKey, TValue>> readFactory, CancellationToken cancellationToken = default) : this(address, port, cancellationToken)
        {
            Task.Factory.StartNew(TcpWrite, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => TcpRead(readFactory), TaskCreationOptions.LongRunning);
        }

        public TcpClientDuplex(IPAddress address, int port, Func<PipeReader, CancellationToken, Task<ITcpPackage<TKey, TValue>>> readFactory, CancellationToken cancellationToken = default) : this(address, port, cancellationToken)
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => TcpReadAsync(readFactory), TaskCreationOptions.LongRunning);
        }

        private TcpClientDuplex(IPAddress address, int port, CancellationToken cancellationToken = default)
        {
            _cancellationToken = cancellationToken;
            _toWrite = new BlockingCollection<byte[]>();
            _output = ImmutableDictionary<TKey, ITcpPackage<TKey, TValue>>.Empty;
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

        public void Send(byte[] data) => _toWrite.Add(data, _cancellationToken);

        public ITcpPackage<TKey, TValue> GetPackage(TKey packageId)
        {
            throw new NotImplementedException();
        }

        private async Task TcpWriteAsync()
        {
            foreach (var bytearray in _toWrite.GetConsumingEnumerable())
            {
                try
                {
                    await Output.WriteAsync(bytearray, _cancellationToken);
                    Output.Advance(bytearray.Length);
                }
                catch (OperationCanceledException)
                {
                    _toWrite.CompleteAdding();
                    _toWrite.Dispose();
                    await Output.CompleteAsync();
                }
            }
        }

        private async Task TcpReadAsync(Func<PipeReader, CancellationToken, Task<ITcpPackage<TKey, TValue>>> readFactory)
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var package = await readFactory(Input, _cancellationToken);
                    //ImmutableInterlocked.InterlockedCompareExchange()
                }
                catch (OperationCanceledException)
                {
                    await Input.CompleteAsync();
                }
            }
        }

        private void TcpWrite() => throw new NotImplementedException();
        private void TcpRead(Func<PipeReader, CancellationToken, ITcpPackage<TKey, TValue>> readFactory) => throw new NotImplementedException();
    }
}