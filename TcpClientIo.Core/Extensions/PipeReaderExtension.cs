using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.TcpClientIo.Extensions
{
    public static class PipeReaderExtension
    {
        public static async Task<ReadResult> ReadLengthAsync(this PipeReader reader, long length, CancellationToken cancellationToken = default)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(length));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readResult = await reader.ReadAsync(cancellationToken);
                
                if (readResult.IsCanceled)
                    throw new OperationCanceledException();

                if (readResult.IsCompleted)
                    throw new EndOfStreamException();

                if (readResult.Buffer.IsEmpty)
                    continue;
                
                var readResultLength = readResult.Buffer.Length;

                if (readResultLength >= length)
                    return readResult;
                
                reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.GetPosition(readResultLength));
            }
        }

        public static void Consume(this PipeReader reader, ReadResult readResult, int length)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(length));
            
            reader.AdvanceTo(readResult.Buffer.GetPosition(length));
        }

        public static ReadOnlySequence<byte> Slice(in this ReadResult readResult, int length, long start = 0) => readResult.Buffer.Slice(start, length);
    }
}