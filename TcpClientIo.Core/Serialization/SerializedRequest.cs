using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization;

internal sealed class SerializedRequest(byte[] rentedArray, int realLength)
{
    internal readonly ReadOnlyMemory<byte> Raw = new(rentedArray, 0, realLength);

    internal void ReturnRentedArray(ArrayPool<byte> pool) => pool.Return(rentedArray);
}
