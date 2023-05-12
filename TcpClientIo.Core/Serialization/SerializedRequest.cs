using System;
using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization
{
    public class SerializedRequest
    {
        private readonly byte[] _rentedArray;
        internal readonly ReadOnlyMemory<byte> Raw;

        internal SerializedRequest(byte[] rentedArray, int realLength)
        {
            _rentedArray = rentedArray;
            Raw = new ReadOnlyMemory<byte>(rentedArray, 0, realLength);
        }

        internal void ReturnRentedArray(ArrayPool<byte> pool, bool clearArray = false) => pool.Return(_rentedArray, clearArray);
    }
}