using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Drenalol.TcpClientIo.Serialization.Pipelines
{
    internal class LoggingPipeWriterExecutor : PipeWriterExecutor
    {
        private readonly ILogger _logger;

        public LoggingPipeWriterExecutor(PipeWriter pipeWriter, ILogger? logger) : base(pipeWriter) => _logger = logger?.ForContext(GetType()) ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");

        public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            var flushResult = await base.WriteAsync(source, cancellationToken);
            _logger.Debug("Completed send: Length: {Length}, IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}", source.Length, flushResult.IsCanceled, flushResult.IsCompleted);

            return flushResult;
        }
    }
}