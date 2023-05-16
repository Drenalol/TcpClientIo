using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization;

internal class TcpSerializerBase
{
    public static readonly ArrayPool<byte> Shared = ArrayPool<byte>.Create();
}