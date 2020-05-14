using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Base;
using Drenalol.Client;
using Drenalol.Helpers;
using Drenalol.Stuff;
using NUnit.Framework;

namespace Drenalol
{
    [Parallelizable]
    [TestFixture(TestOf = typeof(TcpClientIo<,>))]
    public class TcpClientIoTests
    {
        private List<Mock> _mocks;
        private List<Guid> _guids;

        [OneTimeSetUp]
        public void Load()
        {
            _mocks = JsonExt.Deserialize<List<Mock>>(File.ReadAllText("MOCK_DATA_1000")).Select(mock => mock.Build()).ToList();
            _guids = Enumerable.Range(0, 1000000).AsParallel().Select(i => Guid.NewGuid()).ToList();
        }

        [TestCase(1, 1)]
        [TestCase(10, 4)]
        [TestCase(10, -1)]
        [TestCase(100, 4)]
        [TestCase(100, -1)]
        [TestCase(1000, 4)]
        [TestCase(1000, -1)]
        [TestCase(10000, 4)]
        [TestCase(10000, -1)]
        [TestCase(100000, 4)]
        [TestCase(100000, -1)]
        [TestCase(1000000, 1)]
        [TestCase(1000000, 2)]
        [TestCase(1000000, 3)]
        [TestCase(1000000, 4)]
        [TestCase(1000000, -1)]
        public async Task SingleConsumerInParallelTest(int requests, int degree)
        {
            var tcpClient = new TcpClientIo<Mock, Mock>(IPAddress.Any, 10000, new TcpClientIoOptions
            {
                StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 131072),
                StreamPipeWriterOptions = new StreamPipeWriterOptions(minimumBufferSize: 131072)
            });
            var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = degree};
            var sendMs = new ConcurrentBag<long>();
            var receiveMs = new ConcurrentBag<long>();
            var sended = 0;
            var received = 0;

            var sendedGuids = new ConcurrentBag<Guid>();
            var writeTask = Task.Run(() => Parallel.For(0, requests, parallelOptions, Send));
            await Task.WhenAll(writeTask);
            var readTask = Task.Run(() => Parallel.ForEach(sendedGuids, parallelOptions, guid => ReadAsync(guid).GetAwaiter().GetResult()));
            await Task.WhenAll(readTask);

            void Send(int id)
            {
                var sw = Stopwatch.StartNew();
                var mock = _mocks[TestContext.CurrentContext.Random.Next(_mocks.Count)];
                mock.Id = _guids[id];
                sendedGuids.Add(mock.Id);
                tcpClient.Send(mock, CancellationToken.None);
                sw.Stop();
                Interlocked.Increment(ref sended);
                sendMs.Add(sw.ElapsedMilliseconds);
            }

            async Task ReadAsync(Guid guid)
            {
                var sw = Stopwatch.StartNew();
                var package = await tcpClient.ReceiveAsync(guid);
                
                if (package != null)
                {
                    sw.Stop();
                    Interlocked.Increment(ref received);
                    Debug.WriteLine($"Readed {guid} {package.QueueCount.ToString()}");
                }

                receiveMs.Add(sw.ElapsedMilliseconds);
            }

            var readCount = tcpClient.Waiters;
            TestContext.WriteLine($"Send Min Avg Max ms: {sendMs.Min().ToString()} {sendMs.Average().ToString(CultureInfo.CurrentCulture)} {sendMs.Max().ToString()}");
            TestContext.WriteLine($"Receive Min Avg Max ms: {receiveMs.Min().ToString()} {receiveMs.Average().ToString(CultureInfo.CurrentCulture)} {receiveMs.Max().ToString()}");
            TestContext.WriteLine($"Receive > 1 sec: {receiveMs.Count(l => l > 1000).ToString()}");
            TestContext.WriteLine($"Receive > 2 sec: {receiveMs.Count(l => l > 2000).ToString()}");
            TestContext.WriteLine($"Receive > 5 sec: {receiveMs.Count(l => l > 5000).ToString()}");
            TestContext.WriteLine($"Receive > 10 sec: {receiveMs.Count(l => l > 10000).ToString()}");
            TestContext.WriteLine($"Receive > 30 sec: {receiveMs.Count(l => l > 30000).ToString()}");
            TestContext.WriteLine($"SendQueue: {tcpClient.SendQueue.ToString()}");
            TestContext.WriteLine($"ReadCount: {readCount.ToString()}");
            TestContext.WriteLine($"Sended: {sended.ToString()}");
            TestContext.WriteLine($"Received: {received.ToString()}");
            await tcpClient.DisposeAsync();
        }
        
        [TestCase(1000000, 4)]
        public void MultipleConsumersAsyncTest(int requests, int consumers)
        {
            var requestsPerConsumer = requests / consumers;
            var consumersList = Enumerable.Range(0, consumers).Select(i => new TcpClientIo<Mock, Mock>(IPAddress.Parse("192.168.31.95"), 10000, new TcpClientIoOptions
            {
                StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 131072),
                StreamPipeWriterOptions = new StreamPipeWriterOptions(minimumBufferSize: 131072)
            })).ToList();
            var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = 4};
            var sendQueue = 0;
            var receiveQueue = 0;
            var sendMs = new ConcurrentBag<long>();
            var receiveMs = new ConcurrentBag<long>();
            var sended = 0;
            var received = 0;
            var totalSize = 0;
            
            Parallel.ForEach(consumersList, parallelOptions, tcpClient =>
            {
                var sendedGuids = new ConcurrentBag<Guid>();
                var writeTask = Task.Run(() => Parallel.For(0, requestsPerConsumer, parallelOptions, Send));
                Task.WaitAll(writeTask);
                var readTask = Task.Run(() => Parallel.ForEach(sendedGuids, parallelOptions, guid => ReadAsync(guid).GetAwaiter().GetResult()));
                Task.WaitAll(readTask);

                void Send(int id)
                {
                    var sw = Stopwatch.StartNew();
                    var mock = _mocks[TestContext.CurrentContext.Random.Next(_mocks.Count)];
                    mock.Id = _guids[id];
                    sendedGuids.Add(mock.Id);
                    tcpClient.Send(mock, CancellationToken.None);
                    sw.Stop();
                    Interlocked.Increment(ref sended);
                    Interlocked.Add(ref totalSize, mock.Size);
                    sendMs.Add(sw.ElapsedMilliseconds);
                }

                async Task ReadAsync(Guid guid)
                {
                    var sw = Stopwatch.StartNew();
                    var package = await tcpClient.ReceiveAsync(guid);
                
                    if (package != null)
                    {
                        sw.Stop();
                        Interlocked.Increment(ref received);
                        Debug.WriteLine($"Readed {guid} {package.QueueCount.ToString()}");
                    }

                    receiveMs.Add(sw.ElapsedMilliseconds);
                }

                Interlocked.Add(ref sendQueue, tcpClient.SendQueue);
                Interlocked.Add(ref receiveQueue, tcpClient.Waiters);
            });
            
            TestContext.WriteLine($"Send Min Avg Max ms: {sendMs.Min().ToString()} {sendMs.Average().ToString(CultureInfo.CurrentCulture)} {sendMs.Max().ToString()}");
            TestContext.WriteLine($"Receive Min Avg Max ms: {receiveMs.Min().ToString()} {receiveMs.Average().ToString(CultureInfo.CurrentCulture)} {receiveMs.Max().ToString()}");
            TestContext.WriteLine($"Receive > 1 sec: {receiveMs.Count(l => l > 1000).ToString()}");
            TestContext.WriteLine($"Receive > 2 sec: {receiveMs.Count(l => l > 2000).ToString()}");
            TestContext.WriteLine($"Receive > 5 sec: {receiveMs.Count(l => l > 5000).ToString()}");
            TestContext.WriteLine($"Receive > 10 sec: {receiveMs.Count(l => l > 10000).ToString()}");
            TestContext.WriteLine($"Receive > 30 sec: {receiveMs.Count(l => l > 30000).ToString()}");
            TestContext.WriteLine($"SendQueue: {sendQueue.ToString()}");
            TestContext.WriteLine($"ReadCount: {receiveQueue.ToString()}");
            TestContext.WriteLine($"Sended: {sended.ToString()}");
            TestContext.WriteLine($"Received: {received.ToString()}");
            TestContext.WriteLine($"TotalSize: {totalSize.ToString()}");
            /*var requestsPerConsumer = requests / consumers;
            var consumersList = Enumerable.Range(0, consumers).Select(i => new TcpClientIo<Mock, Mock>(IPAddress.Parse("192.168.31.95"), 10000, new TcpClientIoOptions
            {
                StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 131072),
                StreamPipeWriterOptions = new StreamPipeWriterOptions(minimumBufferSize: 131072)
            })).ToList();
            var sendMs = new ConcurrentBag<long>();
            var receiveMs = new ConcurrentBag<long>();
            var sendQueue = 0;
            var receiveQueue = 0;
            var sended = 0;
            var received = 0;

            var tasks = Enumerable.Range(0, consumers).Select(consumer => Task.Run(async () =>
            {
                var tcpClient = consumersList[consumer];
                var sendedGuids = new ConcurrentBag<Guid>();
                var tasks1 = Enumerable.Range(0, requestsPerConsumer).Select(i => Task.Run(() => Send(i))).ToArray();
                await Task.WhenAll(tasks1);
                var guids = sendedGuids.ToArray();
                var tasks2 = Enumerable.Range(0, sendedGuids.Count).Select(i => Task.Run(async () => await ReadAsync(guids[i]))).ToArray();
                await Task.WhenAll(tasks2);

                void Send(int id)
                {
                    var sw = Stopwatch.StartNew();
                    var mock = _mocks[TestContext.CurrentContext.Random.Next(_mocks.Count)];
                    mock.Id = _guids[id];
                    sendedGuids.Add(mock.Id);
                    tcpClient.Send(mock, CancellationToken.None);
                    sw.Stop();
                    Interlocked.Increment(ref sended);
                    sendMs.Add(sw.ElapsedMilliseconds);
                }

                async Task ReadAsync(Guid guid)
                {
                    var sw = Stopwatch.StartNew();
                    var package = await tcpClient.ReceiveAsync(guid);
                
                    if (package != null)
                    {
                        sw.Stop();
                        Interlocked.Increment(ref received);
                    }

                    receiveMs.Add(sw.ElapsedMilliseconds);
                }

            })).ToArray();

            await Task.WhenAll(tasks);

            TestContext.WriteLine($"Send Min Avg Max ms: {sendMs.Min().ToString()} {sendMs.Average().ToString(CultureInfo.CurrentCulture)} {sendMs.Max().ToString()}");
            TestContext.WriteLine($"Receive Min Avg Max ms: {receiveMs.Min().ToString()} {receiveMs.Average().ToString(CultureInfo.CurrentCulture)} {receiveMs.Max().ToString()}");
            TestContext.WriteLine($"Receive > 1 sec: {receiveMs.Count(l => l > 1000).ToString()}");
            TestContext.WriteLine($"Receive > 2 sec: {receiveMs.Count(l => l > 2000).ToString()}");
            TestContext.WriteLine($"Receive > 5 sec: {receiveMs.Count(l => l > 5000).ToString()}");
            TestContext.WriteLine($"Receive > 10 sec: {receiveMs.Count(l => l > 10000).ToString()}");
            TestContext.WriteLine($"Receive > 30 sec: {receiveMs.Count(l => l > 30000).ToString()}");
            TestContext.WriteLine($"SendQueue: {sendQueue.ToString()}");
            TestContext.WriteLine($"ReadCount: {receiveQueue.ToString()}");
            TestContext.WriteLine($"Sended: {sended.ToString()}");
            TestContext.WriteLine($"Received: {received.ToString()}");*/
        }

        [Test]
        public async Task SameIdTest()
        {
            const int requests = 500;
            var list = new List<int>();
            var count = 0;
            var tcpClient = new TcpClientIo<Mock, Mock>(IPAddress.Any, 10000);

            _ = Task.Run(() => Parallel.For(0, requests, i =>
            {
                var mock = _mocks[TestContext.CurrentContext.Random.Next(_mocks.Count)];
                mock.Id = Guid.Empty;
                Assert.IsTrue(tcpClient.TrySend(mock));
            }));

            while (count < requests)
            {
                var delay = TestContext.CurrentContext.Random.Next(1, 200);
                await Task.Delay(delay);
                var packageResult = await tcpClient.ReceiveAsync(Guid.Empty);
                Assert.NotNull(packageResult);
                var queue = packageResult.QueueCount;
                count += queue;

                while (packageResult.TryDequeue(out var package))
                {
                    list.Add(package.Size);
                }

                TestContext.WriteLine($"({count.ToString()}/{requests.ToString()}) +{queue.ToString()}, by {delay.ToString()} ms, SendQueue: {tcpClient.SendQueue.ToString()}, ReadCount: {tcpClient.Waiters.ToString()}");
            }

            var havingCount = list.GroupBy(u => u).Where(p => p.Count() > 1).Select(ig => ig.Key.ToString()).Aggregate((acc, next) => $"{acc}, {next}");
            TestContext.WriteLine($"Non-UNIQ Sizes: {havingCount}");
            await tcpClient.DisposeAsync();
        }

        [Test]
        public async Task DisposeTest()
        {
            var tcpClient = new TcpClientIo<Mock, Mock>(IPAddress.Any, 10000);
            using var timer = new System.Timers.Timer {Interval = 3000};
            timer.Start();
            timer.Elapsed += (sender, args) =>
            {
                ((System.Timers.Timer) sender).Stop();
                tcpClient.DisposeAsync().GetAwaiter().GetResult();
            };
            var mock = _mocks[666];
            while (true)
            {
                try
                {
                    tcpClient.TrySend(mock);
                    await tcpClient.ReceiveAsync(mock.Id);
                }
                catch (Exception e)
                {
                    var exType = e.GetType();
                    Console.WriteLine($"Got Exception: {exType}: {e}");
                    Assert.That(exType == typeof(OperationCanceledException) || exType == typeof(TaskCanceledException));
                    break;
                }
            }
        }

        [Test]
        public async Task CancelSendReceiveTest()
        {
            await using var tcpClient = new TcpClientIo<Mock, Mock>(IPAddress.Any, 10000);
            var mock = _mocks[666];
            var attempts = 0;
            while (attempts < 3)
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                while (true)
                {
                    try
                    {
                        tcpClient.Send(mock, cts.Token);
                        await tcpClient.ReceiveAsync(mock.Id, cts.Token);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Got Exception: {e.GetType()}: {e}");
                        Assert.That(e.GetType() == typeof(OperationCanceledException));
                        attempts++;
                        break;
                    }
                }
            }
        }

        [Test]
        public void LengthTest()
        {
            var mock = new Mock
            {
                Id = Guid.Parse("cc545f85-edd2-411d-8e62-6e1598789a89"),
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

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        public async Task WriteAndWaitResultsAndAfterReceiveTest(int requests)
        {
            var tcpClient = new TcpClientIo<Mock, Mock>(IPAddress.Any, 10000);
            var list = new List<Guid>();
            var writeTasks = Enumerable.Range(0, requests).Select(id => Task.Run(() =>
            {
                var mock = _mocks[TestContext.CurrentContext.Random.Next(_mocks.Count)];
                list.Add(mock.Id);
                tcpClient.Send(mock);
            })).ToArray();
            await Task.WhenAll(writeTasks);
            while (tcpClient.Waiters < requests)
            {
                await Task.Delay(100);
            }

            var receiveTasks = Enumerable.Range(0, requests).Select(id => Task.Run(() => tcpClient.ReceiveAsync(list[id]))).ToArray();
            await Task.WhenAll(receiveTasks);
            Assert.AreEqual(0, tcpClient.SendQueue);
            Assert.AreEqual(0, tcpClient.Waiters);
            await tcpClient.DisposeAsync();
        }

        [Test]
        public void AttributeParsingTest()
        {
            var mock = new AttributeMock();
            var reflectionHelper = new ReflectionHelper<AttributeMock, AttributeMock>();

            foreach (var (_, property) in reflectionHelper.GetRequestProperties())
            {
                var propertyValue = property.Get(mock);
                var bytes = BitConverterHelper.CustomBitConverterToBytes(propertyValue, property.PropertyType);
                var sb = new StringBuilder();
                sb.AppendLine($"{property.PropertyType} {propertyValue ?? "null"}, {bytes?.Length.ToString()} length");
                sb.AppendLine($"\n\tPackageDataType: {property.Attribute.AttributeData.ToString()}");
                sb.AppendLine($"\n\tPackageDataIndex: {property.Attribute.Index.ToString()}");
                sb.AppendLine($"\n\tPackageDataLength: {property.Attribute.Length.ToString()}");
                TestContext.WriteLine(sb);
            }
        }

        [TestCase(1, 1)]
        [TestCase(1000000, 1)]
        [TestCase(1000000, 2)]
        [TestCase(1000000, 3)]
        [TestCase(1000000, 4)]
        [TestCase(1000000, -1)]
        public void AttributeMockSerializeDeserializeTest(int count, int degree)
        {
            var index = 0;
            var serializer = new TcpPackageSerializer<AttributeMockSerialize, AttributeMockSerialize>();
            Parallel.For(0, count, new ParallelOptions {MaxDegreeOfParallelism = degree}, i =>
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
            });
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