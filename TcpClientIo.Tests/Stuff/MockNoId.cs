using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff;

public sealed class MockNoId
{
    [TcpData(0, 4, TcpDataType = TcpDataType.Length)]
    public int Size { get; set; }

    [TcpData(4, TcpDataType = TcpDataType.Body)]
    public string Body { get; set; } = string.Empty;
}
