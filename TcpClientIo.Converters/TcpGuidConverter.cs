using System;

namespace Drenalol.TcpClientIo
{
    public class TcpGuidConverter : TcpConverter<Guid>
    {
        public override byte[] Convert(Guid input) => input.ToByteArray();
        public override Guid ConvertBack(byte[] input) => new Guid(input);
    }
}