using System;
using System.Text;
using Drenalol.Attributes;

namespace Drenalol.Stuff
{
    public struct MockResponse
    {
        [TcpPackageData(0, 16, AttributeData = TcpPackageDataType.Id)]
        public Guid Id { get; set; }

        [TcpPackageData(16, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public int Size { get; set; }

        [TcpPackageData(20, 0, AttributeData = TcpPackageDataType.Body)]
        public byte[] Body { get; set; }
    }
}