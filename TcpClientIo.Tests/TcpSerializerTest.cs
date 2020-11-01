using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;
using NUnit.Framework;

namespace Drenalol.TcpClientIo
{
    public class TcpSerializerTest
    {
        [Test]
        public async Task SerializeDeserializeTest()
        {
            var ethalon = Mock.Default();
            const int ethalonBodyLength = 17238;
            const int ethalonHeaderLength = 270;

            var serializer = new TcpSerializer<long, Mock, Mock>(new List<TcpConverter> {new TcpUtf8StringConverter()}, i => new byte[i]);
            var serialize = serializer.Serialize(ethalon);
            Assert.IsTrue(serialize.Request.Length == ethalonBodyLength + ethalonHeaderLength);
            var (_, deserialize) = await serializer.DeserializeAsync(PipeReader.Create(new MemoryStream(serialize.Request.ToArray())), CancellationToken.None);
            Assert.IsTrue(ethalon.Equals(deserialize));
        }

        [Test]
        public void NotFoundConverterExceptionTest()
        {
            var serializer = new TcpSerializer<long, Mock, Mock>(new List<TcpConverter>(), i => new byte[i]);
            var mock = Mock.Default();
            Assert.Catch<TcpException>(() => serializer.Serialize(mock));
        }

        [TestCase(true, 1, false)]
        [TestCase('c', 2, false)]
        [TestCase(1234.0, 8, false)]
        [TestCase((short) 1234, 2, false)]
        [TestCase(1234, 4, false)]
        [TestCase(1234L, 8, true)]
        [TestCase(1234F, 4, false)]
        [TestCase((ushort) 1234, 2, false)]
        [TestCase(1234U, 4, true)]
        [TestCase(1234UL, 8, true)]
        public void BitConverterToBytesTest(object obj, int expected, bool reverse)
        {
            var converter = new BitConverterHelper(new Dictionary<Type, TcpConverter>());
            Assert.That(converter.ConvertToBytes(obj, obj.GetType()).Length == expected, "converter.ConvertToBytes(obj, obj.GetType()).Length == expected");
        }

        [TestCase(new byte[] {25, 75}, typeof(short), false)]
        [TestCase(new byte[] {0, 1, 2, 5}, typeof(int), false)]
        [TestCase(new byte[] {0, 1, 2, 3, 4, 5, 6, 7}, typeof(long), false)]
        public void BitConverterFromBytesTest(byte[] bytes, Type type, bool reverse)
        {
            var converter = new BitConverterHelper(new Dictionary<Type, TcpConverter>());
            Assert.That(converter.ConvertFromBytes(bytes, type, reverse).GetType() == type, "converter.ConvertFromBytes(bytes, type, reverse).GetType() == type");
        }
    }
}