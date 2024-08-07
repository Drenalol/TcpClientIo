using System;
using System.Buffers;
using System.Linq;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;
using NUnit.Framework;
// ReSharper disable ObjectCreationAsStatement

namespace Drenalol.TcpClientIo
{
    public class TcpStuffTests
    {
        private BitConverterHelper _bitConverterHelper;

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
        private class DoesNotHaveAny
        {
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveBodyAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }

            [TcpData(1, 2, TcpDataType = TcpDataType.Length)]
            public int Length { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveBodyLengthAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }

            [TcpData(1, 2, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class KeyDoesNotHaveSetter
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; }

            [TcpData(1, 2, TcpDataType = TcpDataType.Length)]
            public int Length { get; set; }

            [TcpData(3, 2, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class MetaDataNotHaveSetter
        {
            [TcpData(0, 4)]
            public int Meta { get; }
        }

        [Test]
        public void DoesNotHaveAnyErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DoesNotHaveAny)));
        }
        
        [Test]
        public void DoesNotHaveBodyAttributeErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DoesNotHaveBodyAttribute)));
        }
        
        [Test]
        public void MetaDataNotHaveSetterErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(MetaDataNotHaveSetter)));
        }
        
        [Test]
        public void DoesNotHaveBodyLengthAttributeErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DoesNotHaveBodyLengthAttribute)));
        }
        
        [Test]
        public void KeyDoesNotHaveSetterErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(KeyDoesNotHaveSetter)));
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
                Task.Run(() =>
                {
                    var serialize = serializer.Serialize(mock);
                    _ = deserializer.Deserialize(new ReadOnlySequence<byte>(serialize.Raw));
                });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BaseConvertersTest(bool reverse)
        {
            var str = "Hello my friend";
            var stringResult = _bitConverterHelper.ConvertToSequence(str, typeof(string), reverse);
            var stringResultBack = _bitConverterHelper.ConvertFromSequence(stringResult, typeof(string), reverse);
            Assert.That(str, Is.EqualTo(stringResultBack));

            var datetime = DateTime.Now;
            var dateTimeResult = _bitConverterHelper.ConvertToSequence(datetime, typeof(DateTime), reverse);
            var dateTimeResultBack = _bitConverterHelper.ConvertFromSequence(dateTimeResult, typeof(DateTime), reverse);
            Assert.That(datetime, Is.EqualTo(dateTimeResultBack));

            var guid = Guid.NewGuid();
            var guidResult = _bitConverterHelper.ConvertToSequence(guid, typeof(Guid), reverse);
            var guidResultBack = _bitConverterHelper.ConvertFromSequence(guidResult, typeof(Guid), reverse);
            Assert.That(guid, Is.EqualTo(guidResultBack));
        }
    }
}