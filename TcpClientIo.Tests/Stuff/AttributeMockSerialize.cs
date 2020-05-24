using System;
using Drenalol.Attributes;
using NUnit.Framework;

namespace Drenalol.Stuff
{
    public class AttributeMockSerialize
    {
        [TcpPackageData(0, 4, AttributeData = TcpPackageDataType.Id)]
        public uint Id { get; set; }

        [TcpPackageData(4, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public uint Size { get; set; }

        [TcpPackageData(8, 8)] public ulong LongNumbers { get; set; }

        [TcpPackageData(16, 4)] public uint IntNumbers { get; set; }

        [TcpPackageData(20, 8)] public DateTime DateTime { get; set; }

        [TcpPackageData(28, 60)] public string NotFull { get; set; }

        [TcpPackageData(88, 0, AttributeData = TcpPackageDataType.Body)]
        public byte[] Body { get; set; }

        public void BuildBody()
        {
            NotFull = "TestStringAndNot60Length";
            Body = new byte[TestContext.CurrentContext.Random.Next(10, 1024)];
            TestContext.CurrentContext.Random.NextBytes(Body);
            Size = (uint) Body.Length;
        }
    }
}