using System;
using Drenalol.TcpClientIo.Attributes;
using NUnit.Framework;

namespace Drenalol.TcpClientIo.Stuff
{
    public class AttributeMockSerialize
    {
        [TcpData(0, 4, TcpDataType = TcpDataType.Id)]
        public uint Id { get; set; }

        [TcpData(4, 4, TcpDataType = TcpDataType.Length)]
        public uint Size { get; set; }

        [TcpData(8, 8)]
        public ulong LongNumbers { get; set; }

        [TcpData(16, 4)]
        public uint IntNumbers { get; set; }

        [TcpData(20, 8)]
        public DateTime DateTime { get; set; }

        [TcpData(28, 60)]
        public string NotFull { get; set; }

        [TcpData(88, 0, TcpDataType = TcpDataType.Body)]
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