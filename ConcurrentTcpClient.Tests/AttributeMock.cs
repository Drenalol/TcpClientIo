using System;
using Drenalol.Attributes;
using NUnit.Framework;

namespace Drenalol
{
    public class AttributeMock
    {
        [TcpPackageData(0,2, AttributeData = TcpPackageDataType.Key)]
        public ushort Uint16 { get; set; }

        [TcpPackageData(4, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public uint Uint32 { get; set; }

        [TcpPackageData(8, 8)] public ulong Uint64 { get; set; }
        [TcpPackageData(16, 2)] public short Int16 { get; set; }
        [TcpPackageData(18, 4)] public int Int32 { get; set; }
        [TcpPackageData(22, 8)] public long Int64 { get; set; }
        [TcpPackageData(30, 43, Type = typeof(string))] public object Object { get; set; } = "test123";
        [TcpPackageData(73, 4)] public float Float { get; set; }
        [TcpPackageData(77, 8)] public double Double { get; set; }
        [TcpPackageData(85, 2)] public char Char { get; set; }
        [TcpPackageData(87, 5)] public string String { get; set; } = "test1";
        [TcpPackageData(92, 8)] public DateTime DateTime { get; set; }
        public decimal Ignore { get; set; }

        [TcpPackageData(100, 0, AttributeData = TcpPackageDataType.Body)]
        public byte[] Body { get; set; }

        public AttributeMock()
        {
            Body = new byte[TestContext.CurrentContext.Random.Next(255)];
            TestContext.CurrentContext.Random.NextBytes(Body);
        }
    }
}