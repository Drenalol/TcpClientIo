using System;
using System.Text;

namespace TcpClientDuplex.Extensions
{
    public static class ByteArrayExt
    {
        public static void Merge(this byte[] to, int offsetTo, byte[] from, int fromIndex = 0, int? fromLength = null)
        {
            Array.Copy(from, fromIndex, to, offsetTo, fromLength ?? from.Length);
        }

        public static uint AsUint32(this byte[] bytes)
        {
            var data = new Span<byte>(bytes);
            
            if (!BitConverter.IsLittleEndian)
                data.Reverse();

            return BitConverter.ToUInt32(data);
        }

        public static string AsAsciiString(this byte[] bytes) => Encoding.ASCII.GetString(bytes);
    }
}