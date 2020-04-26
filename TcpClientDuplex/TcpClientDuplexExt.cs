using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TcpClientDuplex
{
    public static class TcpClientDuplexExt
    {
        public static void Merge(this byte[] to, int offsetTo, byte[] from, int fromIndex = 0, int? fromLength = null)
        {
            Array.Copy(from, fromIndex, to, offsetTo, fromLength ?? from.Length);
        }

        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> {new IpAddressJson()}
        };
        
        public static async Task<byte[]> ReadExactlyAsync(this PipeReader reader, long start, long length, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var readResultLength = result.Buffer.Length;

                Debug.Assert(result.Buffer.IsEmpty == false);
                if (readResultLength <= length)
                {
                    reader.AdvanceTo(result.Buffer.Start, result.Buffer.GetPosition(readResultLength));
                    continue;
                }

                var sliceData = result.Buffer.Slice(start, length);
                reader.AdvanceTo(result.Buffer.GetPosition(length));
                return sliceData.ToArray();
            }
            
            throw new AggregateException(nameof(ReadExactlyAsync));
        }

        public static uint AsUint32(this byte[] bytes)
        {
            var data = new Span<byte>(bytes);
            
            if (BitConverter.IsLittleEndian)
                data.Reverse();

            return BitConverter.ToUInt32(data);
        }

        public static string AsAsciiString(this byte[] bytes) => Encoding.ASCII.GetString(bytes);
    }
}