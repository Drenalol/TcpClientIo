using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Abstractions;
using Drenalol.Attributes;
using Drenalol.Base;
using Drenalol.Client;
using Drenalol.Converters;
using Drenalol.Exceptions;
using Drenalol.Helpers;
using Drenalol.Stuff;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Drenalol
{
    public class TcpStuffTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveAny
        {
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveBodyAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }

            [TcpData(1, 2, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }
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

            [TcpData(1, 2, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }

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
        public void ReflectionErrorsTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveAny, DoesNotHaveAny>(NullLogger.Instance));
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<MetaDataNotHaveSetter, MetaDataNotHaveSetter>(NullLogger.Instance));
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveBodyAttribute, DoesNotHaveBodyAttribute>(NullLogger.Instance));
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveBodyLengthAttribute, DoesNotHaveBodyLengthAttribute>(NullLogger.Instance));
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<KeyDoesNotHaveSetter, KeyDoesNotHaveSetter>(NullLogger.Instance));
        }

        [Test]
        public async Task AttributeMockSerializeDeserializeTest()
        {
            var index = 0;
            var serializer = new TcpSerializer<AttributeMockSerialize, AttributeMockSerialize>(new List<TcpConverter>
            {
                new TcpUtf8StringConverter(),
                new TcpDateTimeConverter()
            }, NullLogger.Instance);
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

            var bitConverterHelper = new BitConverterHelper(dict, NullLogger.Instance);

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
            var fakeListener = TcpListener.Create(10000);
            fakeListener.Start();
            TcpClientIo tcpClientIoBase = TcpClientIoTests.GetClient<Mock>();
            var oneMock = (TcpClientIo<Mock>) tcpClientIoBase;
            Assert.NotNull(tcpClientIoBase.GetType().GetMethod(nameof(TcpClientIo.SendAsyncBase)));
            Assert.NotNull(tcpClientIoBase.GetType().GetMethod(nameof(TcpClientIo.ReceiveAsyncBase)));
            Assert.NotNull(oneMock.GetType().GetMethod(nameof(TcpClientIo<object>.SendAsync)));
            Assert.NotNull(oneMock.GetType().GetMethod(nameof(TcpClientIo<object>.ReceiveAsync)));
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            Assert.NotNull(oneMock.GetType().GetMethod(nameof(TcpClientIo<object>.DisposeAsync)));
#else
            Assert.NotNull(oneMock.GetType().GetMethod(nameof(TcpClientIo<object>.Dispose)));
#endif
            TcpClientIo tcpClientIoBase2 = TcpClientIoTests.GetClient<Mock, Mock>();
            var twoMock = (TcpClientIo<Mock, Mock>) tcpClientIoBase2;
            Assert.NotNull(tcpClientIoBase2.GetType().GetMethod(nameof(TcpClientIo.SendAsyncBase)));
            Assert.NotNull(tcpClientIoBase2.GetType().GetMethod(nameof(TcpClientIo.ReceiveAsyncBase)));
            Assert.NotNull(twoMock.GetType().GetMethod(nameof(TcpClientIo<object>.SendAsync)));
            Assert.NotNull(twoMock.GetType().GetMethod(nameof(TcpClientIo<object>.ReceiveAsync)));
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0            
            Assert.NotNull(twoMock.GetType().GetMethod(nameof(TcpClientIo<object>.DisposeAsync)));
#else
            Assert.NotNull(twoMock.GetType().GetMethod(nameof(TcpClientIo<object>.Dispose)));
#endif
            fakeListener.Stop();
            tcpClientIoBase.DisposeBase();
            tcpClientIoBase2.DisposeBase();
        }
    }
}