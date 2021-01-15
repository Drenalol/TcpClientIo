using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff
{
    public class MockNoIdEmptyBody
    {
        [TcpData(0, 2, TcpDataType.Length)]
        public ushort Length { get; set; }

        [TcpData(2, TcpDataType = TcpDataType.Body)]
        public string Empty { get; set; }
    }
}