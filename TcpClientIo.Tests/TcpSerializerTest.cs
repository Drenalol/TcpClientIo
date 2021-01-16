using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;
using NUnit.Framework;

namespace Drenalol.TcpClientIo
{
    public class TcpSerializerTest
    {
        private BitConverterHelper _bitConverterHelper;

        [OneTimeSetUp]
        public void Ctor()
        {
            _bitConverterHelper = new BitConverterHelper(new TcpClientIoOptions().RegisterConverter(new TcpUtf8StringConverter()));
        }

        [Test]
        public void SerializeDeserializeTest()
        {
            var ethalon = Mock.Default();
            const int ethalonHeaderLength = 270;

            var serializer = new TcpSerializer<Mock>(_bitConverterHelper, i => new byte[i]);
            var deserializer = new TcpDeserializer<long, Mock>(_bitConverterHelper);
            var serialize = serializer.Serialize(ethalon);
            Assert.IsTrue(serialize.Request.Length == ethalon.Size + ethalonHeaderLength);
            var (_, deserialize) = deserializer.Deserialize(new ReadOnlySequence<byte>(serialize.Request));
            Assert.IsTrue(ethalon.Equals(deserialize));
        }

        [Test]
        public async Task SerializeDeserializeFromPipeReaderTest()
        {
            var ethalon = Mock.Default();
            const int ethalonHeaderLength = 270;

            var serializer = new TcpSerializer<Mock>(_bitConverterHelper, i => new byte[i]);
            var deserializer = new TcpDeserializer<long, Mock>(_bitConverterHelper);
            var serialize = serializer.Serialize(ethalon);
            Assert.IsTrue(serialize.Request.Length == ethalon.Size + ethalonHeaderLength);
            var (_, deserialize) = await deserializer.DeserializeAsync(PipeReader.Create(new MemoryStream(serialize.Request.ToArray())), CancellationToken.None);
            Assert.IsTrue(ethalon.Equals(deserialize));
        }

        [Test]
        public void NotFoundConverterExceptionTest()
        {
            var serializer = new TcpSerializer<Mock>(new BitConverterHelper(new TcpClientIoOptions()), i => new byte[i]);
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
            var converter = new BitConverterHelper(new TcpClientIoOptions());
            Assert.That(converter.ConvertToBytes(obj, obj.GetType()).Length == expected, "converter.ConvertToBytes(obj, obj.GetType()).Length == expected");
        }

        [TestCase(new byte[] {25, 75}, typeof(short), false)]
        [TestCase(new byte[] {0, 1, 2, 5}, typeof(int), false)]
        [TestCase(new byte[] {0, 1, 2, 3, 4, 5, 6, 7}, typeof(long), false)]
        public void BitConverterFromBytesTest(byte[] bytes, Type type, bool reverse)
        {
            var converter = new BitConverterHelper(new TcpClientIoOptions());
            Assert.That(converter.ConvertFromBytes(new ReadOnlySequence<byte>(bytes), type, reverse).GetType() == type, "converter.ConvertFromBytes(bytes, type, reverse).GetType() == type");
        }

        [Test]
        public void SerializeRecursiveComposeTypeTest()
        {
            var pool = ArrayPool<byte>.Create();
            var serializer = new TcpSerializer<RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<MockOnlyMetaData>>>>>(_bitConverterHelper, i => pool.Rent(i));
            var mock = RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<MockOnlyMetaData>>>>.Create(
                RecursiveMock<RecursiveMock<RecursiveMock<MockOnlyMetaData>>>.Create(
                    RecursiveMock<RecursiveMock<MockOnlyMetaData>>.Create(
                        RecursiveMock<MockOnlyMetaData>.Create(
                            new MockOnlyMetaData()
                        )
                    )
                )
            );
            var serializedRequest = serializer.Serialize(mock);
            serializedRequest.ReturnRentedArrays(pool, false);
        }

        [Test]
        public void SerializeRecursiveStructComposeTypeTest()
        {
            var pool = ArrayPool<byte>.Create();
            var serializer = new TcpSerializer<RecursiveMockStruct<RecursiveMockStruct<RecursiveMockStruct<RecursiveMockStruct<MockOnlyMetaData>>>>>(_bitConverterHelper, i => pool.Rent(i));
            var mock = RecursiveMockStruct<RecursiveMockStruct<RecursiveMockStruct<RecursiveMockStruct<MockOnlyMetaData>>>>.Create(
                RecursiveMockStruct<RecursiveMockStruct<RecursiveMockStruct<MockOnlyMetaData>>>.Create(
                    RecursiveMockStruct<RecursiveMockStruct<MockOnlyMetaData>>.Create(
                        RecursiveMockStruct<MockOnlyMetaData>.Create(
                            new MockOnlyMetaData()
                        )
                    )
                )
            );
            var serializedRequest = serializer.Serialize(mock);
            serializedRequest.ReturnRentedArrays(pool, false);
        }

        [Test]
        public async Task SerializeDeserializeRecursiveComposeTypeTest()
        {
            var pool = ArrayPool<byte>.Create();
            var serializer = new TcpSerializer<RecursiveMock<RecursiveMock<RecursiveMock<MockOnlyMetaData>>>>(_bitConverterHelper, i => pool.Rent(i));
            var deserializer = new TcpDeserializer<long, RecursiveMock<RecursiveMock<RecursiveMock<MockOnlyMetaData>>>>(_bitConverterHelper);
            var mock = RecursiveMock<RecursiveMock<RecursiveMock<MockOnlyMetaData>>>.Create(
                RecursiveMock<RecursiveMock<MockOnlyMetaData>>.Create(
                    RecursiveMock<MockOnlyMetaData>.Create(
                        new MockOnlyMetaData()
                    )
                )
            );
            var serialize = serializer.Serialize(mock);
            var pipe = PipeReader.Create(new MemoryStream(serialize.Request.ToArray()));
            var (id, data) = await deserializer.DeserializeAsync(pipe, CancellationToken.None);
            Assert.NotNull(id);
            Assert.NotNull(data);
        }

        [Test]
        public async Task SerializeDeserializeRecursiveComposeValueTypeTest()
        {
            var pool = ArrayPool<byte>.Create();
            var serializer = new TcpSerializer<RecursiveMock<int>>(_bitConverterHelper, i => pool.Rent(i));
            var deserializer = new TcpDeserializer<long, RecursiveMock<int>>(_bitConverterHelper);
            var mock = RecursiveMock<int>.Create(1337);
            var serialize = serializer.Serialize(mock);
            var pipe = PipeReader.Create(new MemoryStream(serialize.Request.ToArray()));
            var (id, data) = await deserializer.DeserializeAsync(pipe, CancellationToken.None);
            Assert.NotNull(id);
            Assert.NotNull(data);
        }

        [Test]
        public async Task SerializeDeserializeSpeedTest()
        {
            var pool = ArrayPool<byte>.Create();
            var serializer = new TcpSerializer<RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<int>>>>>>(_bitConverterHelper, i => pool.Rent(i));
            var deserializer = new TcpDeserializer<long, RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<int>>>>>>(_bitConverterHelper);
            var mock = RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<int>>>>>.Create(
                RecursiveMock<RecursiveMock<RecursiveMock<RecursiveMock<int>>>>.Create(
                    RecursiveMock<RecursiveMock<RecursiveMock<int>>>.Create(
                        RecursiveMock<RecursiveMock<int>>.Create(
                            RecursiveMock<int>.Create(int.MaxValue
                            )
                        )
                    )
                )
            );
            var sw = Stopwatch.StartNew();

            SerializedRequest serialize = null;
            for (var i = 0; i < 10000; i++)
            {
                serialize = serializer.Serialize(mock);
            }

            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 1000);
            TestContext.WriteLine($"Serialize: {sw.Elapsed.ToString()}");
            sw.Reset();
            
            for (var i = 0; i < 10000; i++)
            {
                var pipe = PipeReader.Create(new MemoryStream(serialize.Request.ToArray()));
                sw.Start();
                var (id, data) = await deserializer.DeserializeAsync(pipe, CancellationToken.None);
                sw.Stop();
                Assert.NotNull(id);
                Assert.NotNull(data);
            }

            Assert.Less(sw.ElapsedMilliseconds, 1000);
            TestContext.WriteLine($"Deserialize: {sw.Elapsed.ToString()}");
        }
    }
}