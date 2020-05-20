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
using Nito.AsyncEx;

namespace Drenalol.Client
{
    [DebuggerDisplay("Id: {_id,nq}, Waiters: {Waiters,nq}")]
    public sealed partial class TcpClientIo<TRequest, TResponse> : IDuplexPipe, IAsyncDisposable where TResponse : new()
    {
        internal readonly Guid _id;
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly TcpClient _tcpClient;
        private readonly BufferBlock<byte[]> _bufferBlockRequests;
        private readonly ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>> _completeResponses;
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly TcpPackageSerializer<TRequest, TResponse> _serializer;
        private readonly SemaphoreSlim _semaphore;
        private readonly PipeReader _deserializePipeReader;
        private readonly PipeWriter _deserializePipeWriter;
        private Exception _internalException;
        private PipeReader _networkStreamPipeReader;
        private PipeWriter _networkStreamPipeWriter;
        PipeReader IDuplexPipe.Input => _networkStreamPipeReader;
        PipeWriter IDuplexPipe.Output => _networkStreamPipeWriter;
        public ulong BytesWrite { get; private set; }
        public ulong BytesRead { get; private set; }
        /// <summary>
        /// WARNING! This property lock internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Waiters => _completeResponses.Count;
        public int Requests => _bufferBlockRequests.Count;

        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null) : this()
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(address, port);
            SetupTcpClient(tcpClientIoOptions);
            SetupTasks();
        }

        private TcpClientIo()
        {
            var pipe = new Pipe();
            _id = Guid.NewGuid();
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _bufferBlockRequests = new BufferBlock<byte[]>();
            _completeResponses = new ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>>();
            _serializer = new TcpPackageSerializer<TRequest, TResponse>();
            _semaphore = new SemaphoreSlim(2, 2);
            _deserializePipeReader = pipe.Reader;
            _deserializePipeWriter = pipe.Writer;
        }

        private void SetupTcpClient(TcpClientIoOptions tcpClientIoOptions)
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);

            var options = tcpClientIoOptions ?? TcpClientIoOptions.Default;
            _tcpClient.SendTimeout = options.TcpClientSendTimeout;
            _tcpClient.ReceiveTimeout = options.TcpClientReceiveTimeout;
            _networkStreamPipeReader = PipeReader.Create(_tcpClient.GetStream(), options.StreamPipeReaderOptions);
            _networkStreamPipeWriter = PipeWriter.Create(_tcpClient.GetStream(), options.StreamPipeWriterOptions);
        }

        private void SetupTasks()
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(TcpReadAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(DeserializeResponseAsync, TaskCreationOptions.LongRunning);
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