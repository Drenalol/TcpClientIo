using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization;

internal class TcpSerializerBase
{
    public static ArrayPool<byte> ArrayPool = null!;
}