using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Extensions
{
    public static class PipeReaderExt
    {
        public static async Task<byte[]> ReadExactlyAsync(this PipeReader reader, long length, CancellationToken cancellationToken = default, long start = 0)
        {
            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var result = await reader.ReadAsync(cancellationToken);

                    var readResultLength = result.Buffer.Length;

                    if (result.Buffer.IsEmpty)
                        continue;

                    if (readResultLength < length)
                    {
                        reader.AdvanceTo(result.Buffer.Start, result.Buffer.GetPosition(readResultLength));
                        continue;
                    }

                    var data = result.Buffer.Slice(start, length).ToArray();
                    reader.AdvanceTo(result.Buffer.GetPosition(length));
                    return data;
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}