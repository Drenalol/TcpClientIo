using System;

namespace Drenalol.Converters
{
    public class TcpPackageDateTimeConverter : ITcpPackageConverter
    {
        public byte[] Convert(object input) => BitConverter.GetBytes(((DateTime) input).ToBinary());

        public object ConvertBack(byte[] input) => DateTime.FromBinary(BitConverter.ToInt64(input));
    }
}