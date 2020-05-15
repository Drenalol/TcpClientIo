using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Base;

namespace Drenalol.Client
{
    public sealed partial class TcpClientIo<TRequest, TResponse> : IDuplexPipe, IAsyncDisposable where TResponse : new()
    {
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly TcpClient _tcpClient;

        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null) : this()
        {
            _tcpClient = tcpClient;
            SetupTcpClient(tcpClientIoOptions);
            SetupTasks();
        }

        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null) : this()
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(address, port);
            SetupTcpClient(tcpClientIoOptions);
            SetupTasks();
        }

        private TcpClientIo()
        {
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _requests = new BufferBlock<byte[]>();
            _completeResponses = new ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>>();
            _serializer = new TcpPackageSerializer<TRequest, TResponse>();
            _semaphore = new SemaphoreSlim(2, 2);
            _responseBlock = new ActionBlock<(object, TResponse)>(AddOrRemoveResponseAsync, new ExecutionDataflowBlockOptions {CancellationToken = _baseCancellationToken});
        }

        private void SetupTcpClient(TcpClientIoOptions tcpClientIoOptions)
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);

            var options = tcpClientIoOptions ?? TcpClientIoOptions.Default;
            Reader = PipeReader.Create(_tcpClient.GetStream(), options.StreamPipeReaderOptions);
            Writer = PipeWriter.Create(_tcpClient.GetStream(), options.StreamPipeWriterOptions);
        }

        private void SetupTasks()
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(TcpReadAsync, TaskCreationOptions.LongRunning);
        }

        public async ValueTask DisposeAsync()
        {
            Debug.WriteLine("Disposing");

            if (_baseCancellationTokenSource != null && !_baseCancellationTokenSource.IsCancellationRequested)
                _baseCancellationTokenSource.Cancel();

            var i = 0;
            while (i++ < 60 && _semaphore.CurrentCount < 2)
            {
                await Task.Delay(100, CancellationToken.None);
            }

            _tcpClient?.Dispose();
            _semaphore?.Dispose();
            Debug.WriteLine("Disposing end");
        }
    }
}