using System;
using System.Text;
using Drenalol.Attributes;

namespace Drenalol.Stuff
{
    public struct MockResponse
    {
        [TcpData(0, 16, TcpDataType = TcpDataType.Id)]
        public Guid Id { get; set; }

        [TcpData(16, 4, TcpDataType = TcpDataType.BodyLength)]
        public int Size { get; set; }

        [TcpData(20, 0, TcpDataType = TcpDataType.Body)]
        public byte[] Body { get; set; }
    }
}