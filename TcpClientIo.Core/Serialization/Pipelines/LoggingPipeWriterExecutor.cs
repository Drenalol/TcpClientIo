using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Serialization.Pipelines
{
    internal class LoggingPipeWriterExecutor : PipeWriterExecutor
    {
        private readonly string _type;
        private readonly ILogger _logger;

        public LoggingPipeWriterExecutor(PipeWriter pipeWriter, string? type, ILogger? logger) : base(pipeWriter)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");
        }

        public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            var flushResult = await base.WriteAsync(source, cancellationToken);
            
            _logger.LogInformation(
                "[{Type:l}] Send: Length: {Length}, IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}",
                _type,
                source.Length,
                flushResult.IsCanceled,
                flushResult.IsCompleted
            );

            return flushResult;
        }
    }
}