using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Drenalol.TcpClientIo.Serialization.Pipelines
{
    internal class LoggingPipeReaderExecutor : PipeReaderExecutor
    {
        private readonly ILogger _logger;

        public LoggingPipeReaderExecutor(PipeReader reader, ILogger? logger) : base(reader) => _logger = logger?.ForContext(GetType()) ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            var readResult = await base.ReadAsync(cancellationToken);
            Log("Intermediate", readResult);
            
            return readResult;
        }

        public override async Task<ReadResult> ReadLengthAsync(long length, CancellationToken cancellationToken = default)
        {
            var readResult = await base.ReadLengthAsync(length, cancellationToken);
            Log("Completed", readResult);

            return readResult;
        }

        private void Log(string stage, in ReadResult readResult) =>
            _logger.Debug(
                "{Stage:l} read: Length: {Length}, IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}",
                stage,
                readResult.Buffer.Length,
                readResult.IsCanceled,
                readResult.IsCompleted
            );
    }
}