using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Abstractions;
using Drenalol.Base;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace Drenalol.Client
{
    /// <summary>
    /// Wrapper of TcpClient what help focus on WHAT you transfer over TCP, not HOW
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    [DebuggerDisplay("Id: {Id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
    public partial class TcpClientIo<TRequest, TResponse> : TcpClientIoBase, IDuplexPipe, IAsyncDisposable where TResponse : new()
#else
    public partial class TcpClientIo<TRequest, TResponse> : TcpClientIoBase, IDuplexPipe, IDisposable where TResponse : new()
#endif
    {
        internal readonly Guid Id;
        private readonly TcpClientIoOptions _options;
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly TcpClient _tcpClient;
        private readonly BufferBlock<byte[]> _bufferBlockRequests;
        private readonly ConcurrentDictionary<object, TaskCompletionSource<ITcpBatch<TResponse>>> _completeResponses;
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly TcpSerializer<TRequest, TResponse> _serializer;
        private readonly SemaphoreSlim _semaphore;
        private readonly PipeReader _deserializePipeReader;
        private readonly PipeWriter _deserializePipeWriter;
        private readonly ILogger<TcpClientIo<TRequest, TResponse>> _logger;
        private Exception _internalException;
        private PipeReader _networkStreamPipeReader;
        private PipeWriter _networkStreamPipeWriter;
        PipeReader IDuplexPipe.Input => _networkStreamPipeReader;
        PipeWriter IDuplexPipe.Output => _networkStreamPipeWriter;
        
        /// <summary>
        /// Gets the number of total bytes written to the <see cref="NetworkStream"/>.
        /// </summary>
        public ulong BytesWrite { get; private set; }
        
        /// <summary>
        /// Gets the number of total bytes read from the <see cref="NetworkStream"/>.
        /// </summary>
        public ulong BytesRead { get; private set; }

        /// <summary>
        /// Gets the number of responses to receive or the number of responses ready to receive.
        /// <para> </para>
        /// WARNING! This property lock whole internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Waiters => _completeResponses.Count;

        /// <summary>
        /// Gets the number of <see cref="TRequest"/> ready to send.
        /// </summary>
        public int Requests => _bufferBlockRequests.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TRequest,TResponse}"/> class and connects to the specified port on the specified host.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TRequest, TResponse>> logger = null) : this(tcpClientIoOptions, logger)
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(address, port);
            SetupTcpClient();
            SetupTasks();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TRequest,TResponse}"/> class and uses connection taken from <see cref="TcpClient"/>
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TRequest, TResponse>> logger = null) : this(tcpClientIoOptions, logger)
        {
            _tcpClient = tcpClient;
            SetupTcpClient();
            SetupTasks();
        }

        private TcpClientIo(TcpClientIoOptions tcpClientIoOptions, ILogger<TcpClientIo<TRequest, TResponse>> logger)
        {
            var pipe = new Pipe();
            Id = Guid.NewGuid();
            _logger = logger;
            _options = tcpClientIoOptions ?? TcpClientIoOptions.Default;
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _bufferBlockRequests = new BufferBlock<byte[]>();
            _completeResponses = new ConcurrentDictionary<object, TaskCompletionSource<ITcpBatch<TResponse>>>();
            _serializer = new TcpSerializer<TRequest, TResponse>(_options.Converters, logger);
            _semaphore = new SemaphoreSlim(2, 2);
            _deserializePipeReader = pipe.Reader;
            _deserializePipeWriter = pipe.Writer;
        }

        private void SetupTcpClient()
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);

            _logger?.LogInformation($"Connected to {(IPEndPoint) _tcpClient.Client.RemoteEndPoint}");
            _tcpClient.SendTimeout = _options.TcpClientSendTimeout;
            _tcpClient.ReceiveTimeout = _options.TcpClientReceiveTimeout;
            _networkStreamPipeReader = PipeReader.Create(_tcpClient.GetStream(), _options.StreamPipeReaderOptions);
            _networkStreamPipeWriter = PipeWriter.Create(_tcpClient.GetStream(), _options.StreamPipeWriterOptions);
        }

        private void SetupTasks()
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(TcpReadAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(DeserializeResponseAsync, TaskCreationOptions.LongRunning);
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public async ValueTask DisposeAsync()
#else
        public void Dispose()
#endif
        {
            _logger?.LogInformation("Dispose started");

            if (_baseCancellationTokenSource != null && !_baseCancellationTokenSource.IsCancellationRequested)
                _baseCancellationTokenSource.Cancel();

            var i = 0;
            while (i++ < 60 && _semaphore.CurrentCount < 2)
            {
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
                await Task.Delay(100, CancellationToken.None);
#else
                Thread.Sleep(100);
#endif
            }

            _tcpClient?.Dispose();
            _semaphore?.Dispose();
            _logger?.LogInformation("Dispose ended");
        }
    }
}