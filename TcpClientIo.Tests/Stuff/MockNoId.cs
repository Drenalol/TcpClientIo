using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff
{
    public class MockNoId
    {
        [TcpData(0, 4, TcpDataType.Length)]
        public int Size { get; set; }

        [TcpData(4, TcpDataType = TcpDataType.Body)]
        public string Body { get; set; }
    }
}