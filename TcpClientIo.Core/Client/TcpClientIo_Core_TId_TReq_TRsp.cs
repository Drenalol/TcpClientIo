using System;
using System.Buffers;
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
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Serialization.Pipelines;
using Drenalol.WaitingDictionary;
using Nito.AsyncEx;
using Serilog;

namespace Drenalol.TcpClientIo.Client
{
    /// <summary>
    /// Wrapper of TcpClient what help focus on WHAT you transfer over TCP, not HOW.
    /// <para>With Identifier version.</para>
    /// </summary>
    /// <typeparam name="TId">Identifier Type in Requests/Responses</typeparam>
    /// <typeparam name="TInput">Request Type</typeparam>
    /// <typeparam name="TOutput">Response Type</typeparam>
    [DebuggerDisplay("Id: {Id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public partial class TcpClientIo<TId, TInput, TOutput> : ITcpClientIo<TId, TInput, TOutput> where TOutput : new() where TId : struct
    {
        [DebuggerNonUserCode]
        private Guid Id { get; }

        private readonly TcpClientIoOptions _options;
        private readonly TcpBatchRules<TOutput> _batchRules;
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly TcpClient _tcpClient = null!;
        private readonly BufferBlock<SerializedRequest> _bufferBlockRequests;
        private readonly WaitingDictionary<TId, ITcpBatch<TOutput>> _completeResponses;
        private readonly TcpSerializer<TInput> _serializer;
        private readonly TcpDeserializer<TId, TOutput> _deserializer;
        private readonly ArrayPool<byte> _arrayPool;
        private readonly AsyncManualResetEvent _writeResetEvent;
        private readonly AsyncManualResetEvent _readResetEvent;
        private readonly AsyncManualResetEvent _consumingResetEvent;
        private readonly PipeReader _deserializePipeReader;
        private readonly PipeWriter _deserializePipeWriter;
        private readonly ILogger? _logger;
        private Exception? _internalException;
        private PipeReader _networkStreamPipeReader = null!;
        private PipeWriter _networkStreamPipeWriter = null!;
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
        /// Initializes a new instance of the <see cref="TcpClientIo{TRequest,TResponse}"/> class and connects to the specified port on the specified host.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(
            IPAddress address,
            int port,
            TcpClientIoOptions? tcpClientIoOptions = null,
            ILogger? logger = null
        ) : this(tcpClientIoOptions, logger)
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
        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions? tcpClientIoOptions = null, ILogger? logger = null) : this(tcpClientIoOptions, logger)
        {
            _tcpClient = tcpClient;
            SetupTcpClient();
            SetupTasks();
        }

        private TcpClientIo(TcpClientIoOptions? tcpClientIoOptions, ILogger? logger)
        {
            var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: long.MaxValue));
            Id = Guid.NewGuid();
            _logger = logger;
            _options = tcpClientIoOptions ?? TcpClientIoOptions.Default;
            _batchRules = TcpBatchRules<TOutput>.Default;
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _bufferBlockRequests = new BufferBlock<SerializedRequest>();
            _completeResponses = new WaitingDictionary<TId, ITcpBatch<TOutput>>(SetupMiddlewareBuilder());
            _arrayPool = ArrayPool<byte>.Create();
            var bitConverterHelper = new BitConverterHelper(_options);
            _serializer = new TcpSerializer<TInput>(bitConverterHelper, length => _arrayPool.Rent(length));
            _writeResetEvent = new AsyncManualResetEvent();
            _readResetEvent = new AsyncManualResetEvent();
            _consumingResetEvent = new AsyncManualResetEvent();
            _deserializePipeReader = pipe.Reader;
            _deserializePipeWriter = pipe.Writer;
            _deserializer = new TcpDeserializer<TId, TOutput>(bitConverterHelper, CreatePipeReaderExecutor(_options.PipeExecutorOptions, _deserializePipeReader));
        }

        private void SetupTcpClient()
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);

            _logger?.Information("Connected to {Endpoint}", (IPEndPoint)_tcpClient.Client.RemoteEndPoint);
            _tcpClient.SendTimeout = _options.TcpClientSendTimeout;
            _tcpClient.ReceiveTimeout = _options.TcpClientReceiveTimeout;
            _networkStreamPipeReader = PipeReader.Create(_tcpClient.GetStream(), _options.StreamPipeReaderOptions);
            _networkStreamPipeWriter = PipeWriter.Create(_tcpClient.GetStream(), _options.StreamPipeWriterOptions);
        }

        private PipeReaderExecutor CreatePipeReaderExecutor(PipeExecutor pipeReaderOptions, PipeReader pipeReader) =>
            pipeReaderOptions switch
            {
                PipeExecutor.Default => new PipeReaderExecutor(pipeReader),
                PipeExecutor.Logging => new LoggingPipeReaderExecutor(pipeReader, _logger),
                _ => throw new ArgumentOutOfRangeException(nameof(pipeReaderOptions), pipeReaderOptions, null)
            };

        private PipeWriterExecutor CreatePipeWriterExecutor(PipeExecutor pipeReaderOptions, PipeWriter pipeWriter) =>
            pipeReaderOptions switch
            {
                PipeExecutor.Default => new PipeWriterExecutor(pipeWriter),
                PipeExecutor.Logging => new LoggingPipeWriterExecutor(pipeWriter, _logger),
                _ => throw new ArgumentOutOfRangeException(nameof(pipeReaderOptions), pipeReaderOptions, null)
            };

        private MiddlewareBuilder<ITcpBatch<TOutput>> SetupMiddlewareBuilder() =>
            new MiddlewareBuilder<ITcpBatch<TOutput>>() { }
                .RegisterCancellationActionInWait(
                    (tcs, hasOwnToken) =>
                    {
                        if (_disposing || hasOwnToken)
                            tcs.TrySetCanceled();
                        else if (!_disposing && _pipelineReadEnded)
                            tcs.TrySetException(TcpClientIoException.ConnectionBroken);
                    }
                )
                .RegisterDuplicateActionInSet((batch, newBatch) => _batchRules.Update(batch, newBatch.Single()))
                .RegisterCompletionActionInSet(() => _consumingResetEvent?.Set());

        private void SetupTasks()
        {
            _ = TcpWriteAsync();
            _ = TcpReadAsync();
            _ = DeserializeResponseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _logger?.Information("Dispose started");
            _disposing = true;

            if (_baseCancellationTokenSource is { IsCancellationRequested: false })
                _baseCancellationTokenSource.Cancel();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var token = cts.Token;
                await _writeResetEvent.WaitAsync(token);
                await _readResetEvent.WaitAsync(token);
            }

            _completeResponses.Dispose();
            _baseCancellationTokenSource.Dispose();
            _tcpClient.Dispose();
            _logger?.Information("Dispose ended");
        }
    }
}