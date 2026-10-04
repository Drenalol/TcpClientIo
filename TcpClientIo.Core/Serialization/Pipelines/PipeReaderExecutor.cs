using System.IO;
using System.IO.Pipelines;

namespace Drenalol.TcpClientIo.Serialization.Pipelines;

internal class PipeReaderExecutor(PipeReader reader)
{
    private readonly PipeReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public async Task<ReadResult> ReadLengthAsync(long length, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1L);

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
