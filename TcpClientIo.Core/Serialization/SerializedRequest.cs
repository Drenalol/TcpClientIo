using System;
using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization
{
    public class SerializedRequest
    {
        internal readonly byte[] RentedArray;
        internal readonly ReadOnlyMemory<byte> Request;

        internal SerializedRequest(byte[] rentedArray, int realLength)
        {
            RentedArray = rentedArray;
            Request = new ReadOnlyMemory<byte>(rentedArray, 0, realLength);
        }

        internal void ReturnRentedArray(ArrayPool<byte> pool, bool clearArray) => pool.Return(RentedArray, clearArray);
    }
}