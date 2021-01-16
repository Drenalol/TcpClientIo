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
        private class ComposeAndBodyAttribute<T>
        {
            [TcpData(1, TcpDataType = TcpDataType.Compose)]
            public T T1 { get; set; }

            [TcpData(2, TcpDataType = TcpDataType.Body)]
            public string T2 { get; set; }
        }
        
        // ReSharper disable once ClassNeverInstantiated.Local
        private class DuplicateComposeAttribute<T>
        {
            [TcpData(1, TcpDataType = TcpDataType.Compose)]
            public T T1 { get; set; }

            [TcpData(2, TcpDataType = TcpDataType.Compose)]
            public T T2 { get; set; }
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
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DoesNotHaveAny), null, _bitConverterHelper));
        }
        
        [Test]
        public void DoesNotHaveBodyAttributeErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DoesNotHaveBodyAttribute), null, _bitConverterHelper));
        }
        
        [Test]
        public void MetaDataNotHaveSetterErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(MetaDataNotHaveSetter), null, _bitConverterHelper));
        }
        
        [Test]
        public void DoesNotHaveBodyLengthAttributeErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DoesNotHaveBodyLengthAttribute), null, _bitConverterHelper));
        }
        
        [Test]
        public void KeyDoesNotHaveSetterErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(KeyDoesNotHaveSetter), null, _bitConverterHelper));
        }
        
        [Test]
        public void DuplicateComposeAttributeErrorTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(DuplicateComposeAttribute<MockOnlyMetaData>), null, _bitConverterHelper));
        }
        
        [Test]
        public void BodyAndComposeAttributeAtSameTimeTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper(typeof(ComposeAndBodyAttribute<MockOnlyMetaData>), null, _bitConverterHelper));
        }
        
        [TestCase(10000, false)]
        [TestCase(10000, true)]
        public async Task AttributeMockSerializeDeserializeTest(int count, bool useParallel)
        {
            var serializer = new TcpSerializer<AttributeMockSerialize>(_bitConverterHelper, i => new byte[i]);
            var deserializer = new TcpDeserializer<uint, AttributeMockSerialize>(_bitConverterHelper);

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
                    _ = deserializer.Deserialize(new ReadOnlySequence<byte>(serialize.Request));
                });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BaseConvertersTest(bool reverse)
        {
            var str = "Hello my friend";
            var stringResult = _bitConverterHelper.ConvertToBytes(str, typeof(string), reverse);
            var stringResultBack = _bitConverterHelper.ConvertFromBytes(new ReadOnlySequence<byte>(stringResult), typeof(string), reverse);
            Assert.AreEqual(str, stringResultBack);

            var datetime = DateTime.Now;
            var dateTimeResult = _bitConverterHelper.ConvertToBytes(datetime, typeof(DateTime), reverse);
            var dateTimeResultBack = _bitConverterHelper.ConvertFromBytes(new ReadOnlySequence<byte>(dateTimeResult), typeof(DateTime), reverse);
            Assert.AreEqual(datetime, dateTimeResultBack);

            var guid = Guid.NewGuid();
            var guidResult = _bitConverterHelper.ConvertToBytes(guid, typeof(Guid), reverse);
            var guidResultBack = _bitConverterHelper.ConvertFromBytes(new ReadOnlySequence<byte>(guidResult), typeof(Guid), reverse);
            Assert.AreEqual(guid, guidResultBack);
        }
    }
}