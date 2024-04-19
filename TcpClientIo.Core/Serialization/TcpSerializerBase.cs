using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization;

internal class TcpSerializerBase
{
    public static readonly ArrayPool<byte> ArrayPool = ArrayPool<byte>.Shared;
}