using System;

namespace Drenalol.TcpClientIo.Converters
{
    /// <summary>
    /// DateTime converter to byte array and vice versa.
    /// </summary>
    public class TcpDateTimeConverter : TcpConverter<DateTime>
    {
        public override byte[] Convert(DateTime input) => BitConverter.GetBytes(input.ToBinary());
        public override DateTime ConvertBack(ReadOnlySpan<byte> input) => DateTime.FromBinary(BitConverter.ToInt64(input));
    }
}