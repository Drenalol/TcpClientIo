using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff
{
    public class MockNoIdEmptyBody
    {
        [TcpData(0, 2, TcpDataType.BodyLength)]
        public ushort Length { get; set; }

        [TcpData(2, TcpDataType = TcpDataType.Body)]
        public byte[] Empty { get; set; }
    }
}