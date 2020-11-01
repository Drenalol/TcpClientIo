using System;

namespace Drenalol.TcpClientIo.Serialization
{
    public class SerializedRequest
    {
        public readonly byte[] RentedArray;
        public readonly ReadOnlyMemory<byte> Request;

        public SerializedRequest(byte[] rentedArray, int realLength)
        {
            RentedArray = rentedArray;
            Request = new ReadOnlyMemory<byte>(RentedArray, 0, realLength);
        }
    }
}