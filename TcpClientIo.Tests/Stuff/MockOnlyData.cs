using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff
{
    public class MockOnlyData
    {
        [TcpData(0, 4)]
        public int Test { get; set; }

        [TcpData(4, 8)]
        public long Long { get; set; }
    }
}