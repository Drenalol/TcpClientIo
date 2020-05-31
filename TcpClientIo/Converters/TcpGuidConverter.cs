using System;
using Drenalol.Abstractions;

namespace Drenalol.Converters
{
    public class TcpGuidConverter : TcpConverter<Guid>
    {
        public override byte[] Convert(Guid input) => input.ToByteArray();
        public override Guid ConvertBack(byte[] input) => new Guid(input);
    }
}