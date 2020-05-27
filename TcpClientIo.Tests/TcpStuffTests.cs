using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Attributes;
using Drenalol.Base;
using Drenalol.Client;
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
            [TcpData(1, 1, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
            [TcpData(2, 1, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        private class DoesNotHaveBodyAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }
            [TcpData(1, 2, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        private class DoesNotHaveBodyLengthAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }
            [TcpData(1, 2, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
        }

        private class KeyDoesNotHaveSetter
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; }
            [TcpData(1, 2, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }
            [TcpData(3, 2, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
        }

        [Test]
        public void ReflectionErrorsTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveAny, DoesNotHaveAny>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveKeyAttribute, DoesNotHaveKeyAttribute>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveBodyAttribute, DoesNotHaveBodyAttribute>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveBodyLengthAttribute, DoesNotHaveBodyLengthAttribute>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<KeyDoesNotHaveSetter, KeyDoesNotHaveSetter>());
        }
        
        [Test]
        public async Task AttributeMockSerializeDeserializeTest()
        {
            var index = 0;
            var serializer = new TcpSerializer<AttributeMockSerialize, AttributeMockSerialize>(new List<TcpConverter>
            {
                new TcpUtf8StringConverter(),
                new TcpDateTimeConverter()
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
            var dict = new Dictionary<Type, TcpConverter>
            {
                {typeof(string), new TcpUtf8StringConverter()},
                {typeof(DateTime), new TcpDateTimeConverter()},
                {typeof(Guid), new TcpGuidConverter()}
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
        public void OopTest()
        {
            TcpClientIoBase tcpClientIoBase = new TcpClientIo<Mock>(IPAddress.Any, 10000);
            var oneMock = (TcpClientIo<Mock>) tcpClientIoBase;
            Assert.IsTrue(tcpClientIoBase.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIoBase.SendAsync)) == 2);
            Assert.IsTrue(tcpClientIoBase.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIoBase.ReceiveAsync)) == 2);
            Assert.IsTrue(oneMock.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIo<object>.SendAsync)) == 2);
            Assert.IsTrue(oneMock.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIo<object>.ReceiveAsync)) == 2);
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            Assert.NotNull(oneMock.GetType().GetMethod(nameof(TcpClientIo<object>.DisposeAsync)));
#else
            Assert.NotNull(oneMock.GetType().GetMethod(nameof(TcpClientIo<object>.Dispose)));
#endif
            TcpClientIoBase tcpClientIoBase2 = new TcpClientIo<Mock, Mock>(IPAddress.Any, 10000);
            var twoMock = (TcpClientIo<Mock, Mock>) tcpClientIoBase2;
            Assert.IsTrue(tcpClientIoBase2.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIoBase.SendAsync)) == 2);
            Assert.IsTrue(tcpClientIoBase2.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIoBase.ReceiveAsync)) == 2);
            Assert.IsTrue(twoMock.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIo<object>.SendAsync)) == 2);
            Assert.IsTrue(twoMock.GetType().GetMethods().Count(method => method.Name == nameof(TcpClientIo<object>.ReceiveAsync)) == 2);
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            Assert.NotNull(twoMock.GetType().GetMethod(nameof(TcpClientIo<object>.DisposeAsync)));
#else
            Assert.NotNull(twoMock.GetType().GetMethod(nameof(TcpClientIo<object>.Dispose)));
#endif
        }
    }
}