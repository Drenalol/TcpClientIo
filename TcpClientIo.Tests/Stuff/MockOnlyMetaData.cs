using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff
{
    public class MockOnlyMetaData
    {
        [TcpData(0, 4)]
        public int Test { get; set; }

        [TcpData(4, 8)]
        public long Long { get; set; }

        public MockOnlyMetaData()
        {
            Test = 5555;
            Long = 12312312;
        }
    }
}