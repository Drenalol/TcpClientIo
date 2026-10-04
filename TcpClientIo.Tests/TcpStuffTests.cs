using System.Buffers;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;
using NUnit.Framework;

namespace Drenalol.TcpClientIo;

public class TcpStuffTests
{
    private BitConverterHelper _bitConverterHelper = null!;

    [OneTimeSetUp]
    public void Ctor()
    {
        _bitConverterHelper = new BitConverterHelper(
            new TcpClientIoOptions()
                .RegisterConverter(new TcpUtf8StringConverter())
                .RegisterConverter(new TcpGuidConverter())
                .RegisterConverter(new TcpDateTimeConverter())
        );
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class DoesNotHaveAny;

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class DoesNotHaveBodyAttribute
    {
        [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
        public int Key { get; set; }

        [TcpData(1, 2, TcpDataType = TcpDataType.Length)]
        public int Length { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class DoesNotHaveBodyLengthAttribute
    {
        [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
        public int Key { get; set; }

        [TcpData(1, 2, TcpDataType = TcpDataType.Body)]
        public int Body { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class KeyDoesNotHaveSetter
    {
        [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
        public int Key { get; }

        [TcpData(1, 2, TcpDataType = TcpDataType.Length)]
        public int Length { get; set; }

        [TcpData(3, 2, TcpDataType = TcpDataType.Body)]
        public int Body { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class MetaDataNotHaveSetter
    {
        [TcpData(0, 4)]
        public int Meta { get; }
    }

    [Test]
    public void DoesNotHaveAnyErrorTest()
    {
        Assert.Throws<TcpException>(() => new ReflectionHelper(typeof(DoesNotHaveAny)));
    }

    [Test]
    public void DoesNotHaveBodyAttributeErrorTest()
    {
        Assert.Throws<TcpException>(() => new ReflectionHelper(typeof(DoesNotHaveBodyAttribute)));
    }

    [Test]
    public void MetaDataNotHaveSetterErrorTest()
    {
        Assert.Throws<TcpException>(() => new ReflectionHelper(typeof(MetaDataNotHaveSetter)));
    }

    [Test]
    public void DoesNotHaveBodyLengthAttributeErrorTest()
    {
        Assert.Throws<TcpException>(() => new ReflectionHelper(typeof(DoesNotHaveBodyLengthAttribute)));
    }

    [Test]
    public void KeyDoesNotHaveSetterErrorTest()
    {
        Assert.Throws<TcpException>(() => new ReflectionHelper(typeof(KeyDoesNotHaveSetter)));
    }

    [TestCase(10000, false)]
    [TestCase(10000, true)]
    public async Task AttributeMockSerializeDeserializeTest(int count, bool useParallel)
    {
        var serializer = new TcpSerializer<AttributeMockSerialize>(_bitConverterHelper, i => new byte[i]);
        var deserializer = new TcpDeserializer<uint, AttributeMockSerialize>(_bitConverterHelper, null!);

        var mock = new AttributeMockSerialize
        {
            Id = TestContext.CurrentContext.Random.NextUInt(),
            DateTime = DateTime.Now.AddSeconds(TestContext.CurrentContext.Random.NextUInt()),
            LongNumbers = TestContext.CurrentContext.Random.NextULong(),
            IntNumbers = TestContext.CurrentContext.Random.NextUInt()
        };

        mock.BuildBody();

        var enumerable = Enumerable.Range(0, count);

        var tasks = (useParallel ? enumerable.AsParallel().Select(Selector) : enumerable.Select(Selector)).ToArray();

        await Task.WhenAll(tasks);

        Task Selector(int i) =>
            Task.Run(
                () =>
                {
                    var serialize = serializer.Serialize(mock);
                    deserializer.Deserialize(new ReadOnlySequence<byte>(serialize.Raw));
                }
            );
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BaseConvertersTest(bool reverse)
    {
        var str = "Hello my friend";
        var stringResult = _bitConverterHelper.ConvertToSequence(str, typeof(string), reverse);
        var stringResultBack = _bitConverterHelper.ConvertFromSequence(stringResult, typeof(string), reverse);
        Assert.That(stringResultBack, Is.EqualTo(str));

        var datetime = DateTime.Now;
        var dateTimeResult = _bitConverterHelper.ConvertToSequence(datetime, typeof(DateTime), reverse);
        var dateTimeResultBack = _bitConverterHelper.ConvertFromSequence(dateTimeResult, typeof(DateTime), reverse);
        Assert.That(dateTimeResultBack, Is.EqualTo(datetime));

        var guid = Guid.NewGuid();
        var guidResult = _bitConverterHelper.ConvertToSequence(guid, typeof(Guid), reverse);
        var guidResultBack = _bitConverterHelper.ConvertFromSequence(guidResult, typeof(Guid), reverse);
        Assert.That(guidResultBack, Is.EqualTo(guid));
    }
}
