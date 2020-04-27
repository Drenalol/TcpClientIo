using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using TcpClientDuplex.Base;

namespace TcpClientDuplex.Extensions
{
    public static class PipeReaderExt
    {
        public static async Task<TcpPackage> ReadFactory(PipeReader reader, CancellationToken cancellationToken)
        {
            var packageId = (await reader.ReadExactlyAsync(4, cancellationToken)).AsUint32();
            var packageSize = (await reader.ReadExactlyAsync(4, cancellationToken)).AsUint32();
            var packageBody = (await reader.ReadExactlyAsync(packageSize, cancellationToken)).AsAsciiString();
            return new TcpPackage(packageId, packageBody);
        }
        
        public static async Task<byte[]> ReadExactlyAsync(this PipeReader reader, long length, CancellationToken cancellationToken = default, long start = 0)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
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
            
            throw new AggregateException(nameof(ReadExactlyAsync));
        }
    }
}