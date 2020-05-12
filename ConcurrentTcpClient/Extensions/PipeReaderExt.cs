using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Extensions
{
    public static class PipeReaderExt
    {
        public static async Task<byte[]> ReadLengthAsync(this PipeReader reader, long length, CancellationToken cancellationToken = default, long start = 0)
        {
            if (length < 1 || start < 0)
                throw new ArgumentOutOfRangeException();
            
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readResult = await reader.ReadAsync(cancellationToken);

                if (readResult.Buffer.IsEmpty)
                    continue;
                
                var readResultLength = readResult.Buffer.Length;

                if (readResultLength < length)
                {
                    reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.GetPosition(readResultLength));
                    continue;
                }

                var data = readResult.Buffer.Slice(start, length).ToArray();
                reader.AdvanceTo(readResult.Buffer.GetPosition(length));
                return data;
            }
        }
    }
}