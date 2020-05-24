using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Attributes;
using Drenalol.Base;
using Drenalol.Converters;
using Drenalol.Exceptions;
using Drenalol.Helpers;
using Drenalol.Stuff;
using NUnit.Framework;

namespace Drenalol
{
    public class TcpStuffTests
    {
        private class DoesNotHaveAny
        {
        }

        private class DoesNotHaveKeyAttribute
        {
            [TcpPackageData(1, 1, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
            [TcpPackageData(2, 1, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        private class DoesNotHaveBodyAttribute
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Id)]
            public int Key { get; set; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        private class DoesNotHaveBodyLengthAttribute
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Id)]
            public int Key { get; set; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
        }

        private class KeyDoesNotHaveSetter
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Id)]
            public int Key { get; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
            [TcpPackageData(3, 2, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
        }

        [Test]
        public void ReflectionErrorsTest()
        {
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveAny, DoesNotHaveAny>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveKeyAttribute, DoesNotHaveKeyAttribute>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveBodyAttribute, DoesNotHaveBodyAttribute>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveBodyLengthAttribute, DoesNotHaveBodyLengthAttribute>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<KeyDoesNotHaveSetter, KeyDoesNotHaveSetter>());
        }
        
        [Test]
        public async Task AttributeMockSerializeDeserializeTest()
        {
            var index = 0;
            var serializer = new TcpPackageSerializer<AttributeMockSerialize, AttributeMockSerialize>(new List<TcpPackageConverter>
            {
                new TcpPackageUtf8StringConverter(),
                new TcpPackageDateTimeConverter()
            });
            var tasks = Enumerable.Range(0, 1000).Select(i => Task.Run(() =>
            {
                var mock = new AttributeMockSerialize
                {
                    Id = TestContext.CurrentContext.Random.NextUInt(),
                    DateTime = DateTime.Now.AddSeconds(TestContext.CurrentContext.Random.NextUInt()),
                    LongNumbers = TestContext.CurrentContext.Random.NextULong(),
                    IntNumbers = TestContext.CurrentContext.Random.NextUInt()
                };
                mock.BuildBody();
                Interlocked.Increment(ref index);
                Debug.WriteLine($"{index.ToString()}: {mock.Size.ToString()} bytes");
                var serialize = serializer.Serialize(mock);
                Assert.IsNotEmpty(serialize);
                var deserialize = serializer.DeserializeAsync(PipeReader.Create(new MemoryStream(serialize)), CancellationToken.None).Result;
                Assert.NotNull(deserialize);
            })).ToArray();

            await Task.WhenAll(tasks);
        }

        [Test]
        public void BaseConvertersTest()
        {
            var dict = new Dictionary<Type, TcpPackageConverter>
            {
                {typeof(string), new TcpPackageUtf8StringConverter()},
                {typeof(DateTime), new TcpPackageDateTimeConverter()},
                {typeof(Guid), new TcpPackageGuidConverter()}
            }.ToImmutableDictionary();
            
            var bitConverterHelper = new BitConverterHelper(dict);
            
            var str = "Hello my friend";
            var stringResult = bitConverterHelper.ConvertToBytes(str, typeof(string));
            var stringResultBack = bitConverterHelper.ConvertFromBytes(stringResult, typeof(string));
            Assert.AreEqual(str, stringResultBack);

            var rts = "dneirf ym olleH";
            var tluseRgnirts = bitConverterHelper.ConvertToBytes(rts, typeof(string), true);
            var kcaBtluseRgnirts = bitConverterHelper.ConvertFromBytes(tluseRgnirts, typeof(string), true);
            Assert.AreEqual(rts, kcaBtluseRgnirts);

            var datetime = DateTime.Now;
            var dateTimeResult = bitConverterHelper.ConvertToBytes(datetime, typeof(DateTime));
            var dateTimeResultBack = bitConverterHelper.ConvertFromBytes(dateTimeResult, typeof(DateTime));
            Assert.AreEqual(datetime, dateTimeResultBack);

            var guid = Guid.NewGuid();
            var guidResult = bitConverterHelper.ConvertToBytes(guid, typeof(Guid));
            var guidResultBack = bitConverterHelper.ConvertFromBytes(guidResult, typeof(Guid));
            Assert.AreEqual(guid, guidResultBack);
        }
        
        [Test]
        public void Test()
        {
        }
    }
}