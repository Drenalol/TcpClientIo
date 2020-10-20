using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace Drenalol.TcpClientIo
{
    /// <summary>
    /// Wrapper of TcpClient what help focus on WHAT you transfer over TCP, not HOW.
    /// <para>With Identifier version.</para>
    /// </summary>
    /// <typeparam name="TId">Identifier Type in Requests/Responses</typeparam>
    /// <typeparam name="TRequest">Request Type</typeparam>
    /// <typeparam name="TResponse">Response Type</typeparam>
    [DebuggerDisplay("Id: {Id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public partial class TcpClientIo<TId, TRequest, TResponse> : ITcpClientIo<TId, TRequest, TResponse> where TResponse : new() where TId : struct
    {
        [DebuggerNonUserCode]
        private Guid Id { get; }

        private readonly TcpClientIoOptions _options;
        private readonly TcpBatchRules<TId, TResponse> _batchRules;
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly TcpClient _tcpClient;
        private readonly BufferBlock<byte[]> _bufferBlockRequests;
        private readonly ConcurrentDictionary<TId, TaskCompletionSource<ITcpBatch<TId, TResponse>>> _completeResponses;
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly TcpSerializer<TId, TRequest, TResponse> _serializer;
        private readonly AsyncManualResetEvent _writeResetEvent;
        private readonly AsyncManualResetEvent _readResetEvent;
        private readonly AsyncManualResetEvent _consumingResetEvent;
        private readonly PipeReader _deserializePipeReader;
        private readonly PipeWriter _deserializePipeWriter;
        private readonly ILogger<TcpClientIo<TId, TRequest, TResponse>> _logger;
        private Exception _internalException;
        private PipeReader _networkStreamPipeReader;
        private PipeWriter _networkStreamPipeWriter;
        private bool _disposing;
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
        /// WARNING! This property lock whole internal <see cref="ConcurrentDictionary{TId,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Waiters => _completeResponses.Count;

        /// <summary>
        /// Gets the number of <see cref="TRequest"/> ready to send.
        /// </summary>
        public int Requests => _bufferBlockRequests.Count;

        /// <summary>
        /// Gets an immutable snapshot of responses to receive (id, null) or responses ready to receive (id, <see cref="ITcpBatch{TId, TResponse}"/>).
        /// </summary>
        public ImmutableDictionary<TId, ITcpBatch<TId, TResponse>> GetWaiters()
            => _completeResponses
                .ToArray()
                .ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Task.Status == TaskStatus.RanToCompletion ? pair.Value.Task.Result : null);

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TRequest,TResponse}"/> class and connects to the specified port on the specified host.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TId, TRequest, TResponse>> logger = null) : this(tcpClientIoOptions, logger)
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
        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TId, TRequest, TResponse>> logger = null) : this(tcpClientIoOptions, logger)
        {
            _tcpClient = tcpClient;
            SetupTcpClient();
            SetupTasks();
        }

        private TcpClientIo(TcpClientIoOptions tcpClientIoOptions, ILogger<TcpClientIo<TId, TRequest, TResponse>> logger)
        {
            var pipe = new Pipe();
            Id = Guid.NewGuid();
            _logger = logger;
            _options = tcpClientIoOptions ?? TcpClientIoOptions.Default;
            _batchRules = TcpBatchRules<TId, TResponse>.Default;
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _bufferBlockRequests = new BufferBlock<byte[]>();
            _completeResponses = new ConcurrentDictionary<TId, TaskCompletionSource<ITcpBatch<TId, TResponse>>>();
            _serializer = new TcpSerializer<TId, TRequest, TResponse>(_options.Converters);
            _writeResetEvent = new AsyncManualResetEvent();
            _readResetEvent = new AsyncManualResetEvent();
            _consumingResetEvent = new AsyncManualResetEvent();
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
            Task.Run(TcpWriteAsync, CancellationToken.None);
            Task.Run(TcpReadAsync, CancellationToken.None);
            Task.Run(DeserializeResponseAsync, CancellationToken.None);
        }

#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public async ValueTask DisposeAsync()
#else
        public void Dispose()
#endif
        {
            _logger?.LogInformation("Dispose started");
            _disposing = true;

            if (_baseCancellationTokenSource != null && !_baseCancellationTokenSource.IsCancellationRequested)
                _baseCancellationTokenSource.Cancel();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var token = cts.Token;
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
                await _writeResetEvent.WaitAsync(token);
#else
                _writeResetEvent.Wait(token);
#endif

#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
                await _readResetEvent.WaitAsync(token);
#else
                _readResetEvent.Wait(token);
#endif
            }

            _baseCancellationTokenSource?.Dispose();
            _completeResponses?.Clear();
            _tcpClient?.Dispose();
            _logger?.LogInformation("Dispose ended");
        }
    }
}