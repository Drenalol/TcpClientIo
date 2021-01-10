using System;
using System.Buffers;
using System.Collections.Generic;

namespace Drenalol.TcpClientIo.Serialization
{
    public class SerializedRequest
    {
        internal readonly int RealLength;
        internal readonly byte[] RentedArray;
        internal readonly IEnumerable<byte[]> ComposeRentedArrays;
        internal readonly ReadOnlyMemory<byte> Request;

        internal SerializedRequest(byte[] rentedArray, int realLength, IEnumerable<byte[]> composeRentedArrays = null)
        {
            RentedArray = rentedArray;
            RealLength = realLength;
            ComposeRentedArrays = composeRentedArrays;
            Request = new ReadOnlyMemory<byte>(rentedArray, 0, realLength);
        }

        internal void ReturnRentedArrays(ArrayPool<byte> pool, bool clearArray)
        {
            pool.Return(RentedArray, clearArray);

            if (ComposeRentedArrays == null)
                return;

            foreach (var rentedArray in ComposeRentedArrays)
                pool.Return(rentedArray, clearArray);
        }
    }
}