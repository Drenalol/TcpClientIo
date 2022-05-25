using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Drenalol.TcpClientIo.Serialization.Pipelines
{
    internal class LoggingPipeReaderExecutor : PipeReaderExecutor
    {
        private readonly string? _type;
        private readonly ILogger _logger;

        public LoggingPipeReaderExecutor(PipeReader reader, string? type, ILogger? logger) : base(reader)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _logger = logger?.ForContext(GetType()) ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            var readResult = await base.ReadAsync(cancellationToken);
            
            _logger.Debug(
                "[{Type:l}] Read: Length: {Length}, IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}",
                _type,
                readResult.Buffer.Length,
                readResult.IsCanceled,
                readResult.IsCompleted
            );
            
            return readResult;
        }
    }
}