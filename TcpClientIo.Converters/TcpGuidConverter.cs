using System;

namespace TcpClientIo.Converters
{
    public class TcpGuidConverter : TcpConverter<Guid>
    {
        public override byte[] Convert(Guid input) => input.ToByteArray();
        public override Guid ConvertBack(byte[] input) => new Guid(input);
    }
}