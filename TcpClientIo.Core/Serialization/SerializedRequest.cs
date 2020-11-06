using System;

namespace Drenalol.TcpClientIo.Serialization
{
    public class SerializedRequest
    {
        internal readonly byte[] RentedArray;
        internal readonly ReadOnlyMemory<byte> Request;

        internal SerializedRequest(byte[] rentedArray, int realLength)
        {
            RentedArray = rentedArray;
            Request = new ReadOnlyMemory<byte>(RentedArray, 0, realLength);
        }
    }
}