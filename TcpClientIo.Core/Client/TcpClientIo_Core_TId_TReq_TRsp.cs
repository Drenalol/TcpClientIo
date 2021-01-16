using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.TcpClientIo.Batches;
using Drenalol.TcpClientIo.Contracts;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.WaitingDictionary;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace Drenalol.TcpClientIo.Client
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
        private readonly TcpBatchRules<TResponse> _batchRules;
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly TcpClient _tcpClient;
        private readonly BufferBlock<SerializedRequest> _bufferBlockRequests;
        private readonly WaitingDictionary<TId, ITcpBatch<TResponse>> _completeResponses;
        private readonly TcpSerializer<TRequest> _serializer;
        private readonly TcpDeserializer<TId, TResponse> _deserializer;
        private readonly ArrayPool<byte> _arrayPool;
        private readonly AsyncManualResetEvent _writeResetEvent;
        private readonly AsyncManualResetEvent _readResetEvent;
        private readonly AsyncManualResetEvent _consumingResetEvent;
        private readonly PipeReader _deserializePipeReader;
        private readonly PipeWriter _deserializePipeWriter;
        private readonly ILogger<TcpClientIo<TId, TRequest, TResponse>> _logger;
        private Exception _internalException;
        private PipeReader _networkStreamPipeReader;
        private PipeWriter _networkStreamPipeWriter;
        private bool _pipelineReadEnded;
        private bool _pipelineWriteEnded;
        private bool _disposing;
        private long _bytesWrite;
        private long _bytesRead;
        PipeReader IDuplexPipe.Input => _networkStreamPipeReader;
        PipeWriter IDuplexPipe.Output => _networkStreamPipeWriter;

        /// <summary>
        /// Gets the number of total bytes written to the <see cref="NetworkStream"/>.
        /// </summary>
        public long BytesWrite => _bytesWrite;

        /// <summary>
        /// Gets the number of total bytes read from the <see cref="NetworkStream"/>.
        /// </summary>
        public long BytesRead => _bytesRead;

        /// <summary>
        /// Gets the number of responses to receive or the number of responses ready to receive.
        /// </summary>
        public int Waiters => _completeResponses.Count;

        /// <summary>
        /// Gets the number of requests ready to send.
        /// </summary>
        public int Requests => _bufferBlockRequests.Count;

        /// <summary>
        /// Gets an immutable snapshot of responses to receive (id, null) or responses ready to receive (id, <see cref="ITcpBatch{TResponse}"/>).
        /// </summary>
        public ImmutableDictionary<TId, WaiterInfo<ITcpBatch<TResponse>>> GetWaiters()
            => _completeResponses
                .ToImmutableDictionary(pair => pair.Key, pair => new WaiterInfo<ITcpBatch<TResponse>>(pair.Value.Task));

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
            _batchRules = TcpBatchRules<TResponse>.Default;
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _bufferBlockRequests = new BufferBlock<SerializedRequest>();
            var middleware = new MiddlewareBuilder<ITcpBatch<TResponse>>()
                .RegisterCancellationActionInWait((tcs, hasOwnToken) =>
                {
                    if (_disposing || hasOwnToken)
                        tcs.TrySetCanceled();
                    else if (!_disposing && _pipelineReadEnded)
                        tcs.TrySetException(TcpClientIoException.ConnectionBroken());
                })
                .RegisterDuplicateActionInSet((batch, newBatch) => _batchRules.Update(batch, newBatch.Single()))
                .RegisterCompletionActionInSet(() => _consumingResetEvent.Set());
            _completeResponses = new WaitingDictionary<TId, ITcpBatch<TResponse>>(middleware);
            _arrayPool = ArrayPool<byte>.Create();
            var bitConverterHelper = new BitConverterHelper(_options);
            _serializer = new TcpSerializer<TRequest>(bitConverterHelper, length => _arrayPool.Rent(length));
            _deserializer = new TcpDeserializer<TId, TResponse>(bitConverterHelper);
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
            _ = TcpWriteAsync();
            _ = TcpReadAsync().ContinueWith(antecedent =>
            {
                if (_disposing || !_pipelineReadEnded)
                    return;

                foreach (var kv in _completeResponses.ToArray())
                    kv.Value.TrySetException(TcpClientIoException.ConnectionBroken());
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
            _ = DeserializeResponseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _logger?.LogInformation("Dispose started");
            _disposing = true;

            if (_baseCancellationTokenSource != null && !_baseCancellationTokenSource.IsCancellationRequested)
                _baseCancellationTokenSource.Cancel();

            _completeResponses?.Dispose();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var token = cts.Token;
                await _writeResetEvent.WaitAsync(token);
                await _readResetEvent.WaitAsync(token);
            }

            _baseCancellationTokenSource?.Dispose();
            _tcpClient?.Dispose();
            _logger?.LogInformation("Dispose ended");
        }
    }
}