using System;
using Drenalol.Abstractions;
using Drenalol.Base;

namespace Drenalol.Converters
{
    public class TcpDateTimeConverter : TcpConverter<DateTime>
    {
        public override byte[] Convert(DateTime input) => BitConverter.GetBytes(input.ToBinary());
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public override DateTime ConvertBack(byte[] input) => DateTime.FromBinary(BitConverter.ToInt64(input));
#else
        public override DateTime ConvertBack(byte[] input) => DateTime.FromBinary(BitConverter.ToInt64(input, 0));
#endif
    }
}