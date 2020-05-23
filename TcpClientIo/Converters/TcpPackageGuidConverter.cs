using System;
using Drenalol.Base;

namespace Drenalol.Converters
{
    public class TcpPackageGuidConverter : TcpPackageConverter<Guid>
    {
        public override byte[] Convert(Guid input) => input.ToByteArray();
        public override Guid ConvertBack(byte[] input) => new Guid(input);
    }
}