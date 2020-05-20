using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Attributes;
using Drenalol.Base;
using Drenalol.Exceptions;
using Drenalol.Helpers;
using Drenalol.Stuff;
using NUnit.Framework;

namespace Drenalol
{
    public class TcpStuffTests
    {
        class DoesNotHaveAny
        {
        }
        
        class DoesNotHaveKeyAttribute
        {
            [TcpPackageData(1, 1, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
            [TcpPackageData(2, 1, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        class DoesNotHaveBodyAttribute
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Key)]
            public int Key { get; set; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
        }
        
        class DoesNotHaveBodyLengthAttribute
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Key)]
            public int Key { get; set; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
        }

        class KeyDoesNotHaveSetter
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Key)]
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
            var serializer = new TcpPackageSerializer<AttributeMockSerialize, AttributeMockSerialize>();
            var tasks = Enumerable.Range(0, 1000000).Select(i => Task.Run(() =>
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
        public void LengthTest()
        {
            var mock = new Mock
            {
                Id = 123,
                FirstName = "Iggy",
                LastName = "Kopelman",
                Email = "ikopelman0@independent.co.uk",
                Gender = "Male",
                IpAddress = "120.243.0.112",
                Data = "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c" +
                       "5907081e0aa3851f7ecf497b783d528c"
            };
            var mockS = JsonExt.Serialize(mock);
            var mockD = JsonExt.Deserialize<Mock>(mockS);
            var mockS2 = JsonExt.Serialize(mockD);
            TestContext.WriteLine(mockS.Length);
            TestContext.WriteLine(mockS2.Length);
            Assert.AreEqual(mockS2.Length, mockS.Length);
        }

        [Test]
        public void BaseConvertersTest()
        {
            const string str = "Hello my friend";
            TcpPackageDataConverters.TryConvert(typeof(string), str, out var stringResult);
            TcpPackageDataConverters.TryConvertBack(typeof(string), stringResult, out var stringResultBack);
            Assert.AreEqual(str, stringResultBack);

            var datetime = DateTime.Now;
            TcpPackageDataConverters.TryConvert(typeof(DateTime), datetime, out var dateTimeResult);
            TcpPackageDataConverters.TryConvertBack(typeof(DateTime), dateTimeResult, out var dateTimeResultBack);
            Assert.AreEqual(datetime, dateTimeResultBack);

            var fake = 1337U;
            TcpPackageDataConverters.TryConvert(typeof(uint), fake, out var fakeResult);
            TcpPackageDataConverters.TryConvertBack(typeof(uint), fakeResult, out var fakeResultBack);
            Assert.AreNotEqual(fake, fakeResultBack);
        }
    }
}