using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Extensions
{
    internal class PipeReaderExecutor
    {
        private readonly PipeReader _reader;

        public PipeReaderExecutor(PipeReader reader) => _reader = reader;

        public virtual async Task<ReadResult> ReadLengthAsync(long length, CancellationToken cancellationToken = default)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(length));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readResult = await ReadAsync(cancellationToken);
                
                if (readResult.IsCanceled)
                    throw new OperationCanceledException();

                if (readResult.IsCompleted)
                    throw new EndOfStreamException();

                if (readResult.Buffer.IsEmpty)
                    continue;
                
                var readResultLength = readResult.Buffer.Length;

                if (readResultLength >= length)
                    return readResult;
                
                Examine(readResult.Buffer.Start, readResult.Buffer.GetPosition(readResultLength));
            }
        }

        public virtual ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) => _reader.ReadAsync(cancellationToken); 

        public void Consume(SequencePosition consume) => _reader.AdvanceTo(consume);
        
        public void Examine(SequencePosition consumed, SequencePosition examined) => _reader.AdvanceTo(consumed, examined);
    }

    internal class LoggedPipeReaderExecutor : PipeReaderExecutor
    {
        private readonly ILogger _logger;

        public LoggedPipeReaderExecutor(PipeReader reader, ILogger? logger) : base(reader) => _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");

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
            _logger.LogDebug(
                "{Stage} read: IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}, IsEmpty: {IsEmpty}, Length: {Length}",
                stage,
                readResult.IsCanceled,
                readResult.IsCompleted,
                readResult.Buffer.IsEmpty,
                readResult.Buffer.Length
            );
    }
}