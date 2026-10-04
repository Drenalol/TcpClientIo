using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Serialization.Pipelines;
using Drenalol.TcpClientIo.Stuff;
using NUnit.Framework;

namespace Drenalol.TcpClientIo;

public class TcpSerializerTest
{
    private BitConverterHelper _bitConverterHelper = null!;

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
        var deserializer = new TcpDeserializer<long, Mock>(_bitConverterHelper, null!);
        var serialize = serializer.Serialize(ethalon);
        Assert.That(serialize.Raw.Length, Is.EqualTo(ethalon.Size + ethalonHeaderLength));
        var (_, deserialize) = deserializer.Deserialize(new ReadOnlySequence<byte>(serialize.Raw));
        Assert.That(deserialize, Is.EqualTo(ethalon));
    }

    [Test]
    public async Task SerializeDeserializeFromPipeReaderTest()
    {
        var ethalon = Mock.Default();
        const int ethalonHeaderLength = 270;

        var serializer = new TcpSerializer<Mock>(_bitConverterHelper, i => new byte[i]);
        var serialize = serializer.Serialize(ethalon);
        var deserializer = new TcpDeserializer<long, Mock>(_bitConverterHelper, new PipeReaderExecutor(PipeReader.Create(new MemoryStream(serialize.Raw.ToArray()))));
        Assert.That(serialize.Raw.Length, Is.EqualTo(ethalon.Size + ethalonHeaderLength));
        var (_, deserialize) = await deserializer.DeserializePipeAsync(CancellationToken.None);
        Assert.That(deserialize, Is.EqualTo(ethalon));
    }

    [Test]
    public void NotFoundConverterExceptionTest()
    {
        var serializer = new TcpSerializer<Mock>(new BitConverterHelper(new TcpClientIoOptions()), i => new byte[i]);
        var mock = Mock.Default();
        Assert.Throws<TcpException>(() => serializer.Serialize(mock));
    }

    [TestCase(true, 1)]
    [TestCase('c', 2)]
    [TestCase(1234.0, 8)]
    [TestCase((short)1234, 2)]
    [TestCase(1234, 4)]
    [TestCase(1234L, 8)]
    [TestCase(1234F, 4)]
    [TestCase((ushort)1234, 2)]
    [TestCase(1234U, 4)]
    [TestCase(1234UL, 8)]
    public void BitConverterToBytesTest(object obj, int expected)
    {
        var converter = new BitConverterHelper(new TcpClientIoOptions());
        Assert.That(converter.ConvertToSequence(obj, obj.GetType()).Length, Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 25, 75 }, typeof(short))]
    [TestCase(new byte[] { 0, 1, 2, 5 }, typeof(int))]
    [TestCase(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, typeof(long))]
    public void BitConverterFromBytesTest(byte[] bytes, Type type)
    {
        var converter = new BitConverterHelper(new TcpClientIoOptions());
        Assert.That(converter.ConvertFromSequence(new ReadOnlySequence<byte>(bytes), type, false).GetType(), Is.EqualTo(type));
    }

    [Test]
    public async Task SerializeDeserializeSpeedTest()
    {
        var pool = TcpSerializerBase.ArrayPool;
        var serializer = new TcpSerializer<Mock>(_bitConverterHelper, i => pool.Rent(i));
        var mock = Mock.Default();
        var sw = Stopwatch.StartNew();

        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: long.MaxValue));
        for (var i = 0; i < 10000; i++)
        {
            var serialize = serializer.Serialize(mock);
            await pipe.Writer.WriteAsync(serialize.Raw);
        }

        sw.Stop();
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
        TestContext.WriteLine($"Serialize: {sw.Elapsed}");
        sw.Reset();

        var deserializer = new TcpDeserializer<long, Mock>(_bitConverterHelper, new PipeReaderExecutor(pipe.Reader));

        var maxDeserializeMs = 0L;
        for (var i = 0; i < 10000; i++)
        {
            sw.Start();
            var (id, data) = await deserializer.DeserializePipeAsync(CancellationToken.None);
            sw.Stop();
            Assert.That(id, Is.Not.EqualTo(0L));
            Assert.That(data, Is.Not.Null);
            maxDeserializeMs = Math.Max(maxDeserializeMs, sw.ElapsedMilliseconds);
        }

        Assert.That(maxDeserializeMs, Is.LessThan(1000));
        TestContext.WriteLine($"Deserialize: worst iteration: {maxDeserializeMs} ms");
    }
}
