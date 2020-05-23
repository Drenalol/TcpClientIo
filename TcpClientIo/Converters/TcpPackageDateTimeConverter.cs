using System;
using Drenalol.Base;

namespace Drenalol.Converters
{
    public class TcpPackageDateTimeConverter : TcpPackageConverter<DateTime>
    {
        public override byte[] Convert(DateTime input) => BitConverter.GetBytes(input.ToBinary());
        public override DateTime ConvertBack(byte[] input) => DateTime.FromBinary(BitConverter.ToInt64(input));
    }
}