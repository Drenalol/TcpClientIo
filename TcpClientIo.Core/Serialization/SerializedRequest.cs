using System;
using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization
{
    public class SerializedRequest
    {
        internal readonly int RealLength;
        internal readonly byte[] RentedArray;
        internal readonly ReadOnlyMemory<byte> Request;
        internal readonly SerializedRequest LinkedSerializedRequest;

        internal SerializedRequest(byte[] rentedArray, int realLength, SerializedRequest linkedSerializedRequest = null)
        {
            RentedArray = rentedArray;
            RealLength = realLength;
            LinkedSerializedRequest = linkedSerializedRequest;
            Request = new ReadOnlyMemory<byte>(rentedArray, 0, realLength);
        }

        internal void ReturnRentedArrays(ArrayPool<byte> pool, bool clearArray)
        {
            pool.Return(RentedArray, clearArray);
            LinkedSerializedRequest?.ReturnRentedArrays(pool, clearArray);
        }
    }
}