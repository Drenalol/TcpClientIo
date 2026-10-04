using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff;

public sealed class MockOnlyMetaData
{
    [TcpData(0, 4)]
    public int Test { get; set; } = 5555;

    [TcpData(4, 8)]
    public long Long { get; set; } = 12312312;
}
