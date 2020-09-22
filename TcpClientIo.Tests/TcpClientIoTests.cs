using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Abstractions;
using Drenalol.Base;
using Drenalol.Client;
using Drenalol.Converters;
using Drenalol.Stuff;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Drenalol
{
    [TestFixture(TestOf = typeof(TcpClientIo<,>))]
    public class TcpClientIoTests
    {
        public static readonly IPAddress IpAddress = Dns.GetHostAddresses("yanysh.com")[0];// IPAddress.Any;
        public static ImmutableList<Mock> Mocks;

        private static (TcpClientIoOptions, ILoggerFactory) GetDefaults(LogLevel logLevel)
        {
            var options = TcpClientIoOptions.Default;

            options.Converters = new List<TcpConverter>
            {
                new TcpGuidConverter(),
                new TcpDateTimeConverter(),
                new TcpUtf8StringConverter()
            };
            
            options.StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 10240000);
            options.StreamPipeWriterOptions = new StreamPipeWriterOptions();

            var loggerFactory = LoggerFactory.Create(lb =>
            {
                lb.AddFilter("Drenalol.Client.TcpClientIo", logLevel);
                lb.AddDebug();
                lb.AddConsole();
            });

            return (options, loggerFactory);
        }

        public static TcpClientIo<T, T> GetClient<T>(IPAddress ipAddress = null, LogLevel logLevel = LogLevel.Warning) where T : new()
        {
            var (options, loggerFactory) = GetDefaults(logLevel);
            return new TcpClientIo<T>(ipAddress ?? IpAddress, 10000, options, loggerFactory.CreateLogger<TcpClientIo<T>>());
        }

        public static TcpClientIo<T, TR> GetClient<T, TR>(IPAddress ipAddress = null, LogLevel logLevel = LogLevel.Warning) where TR : new()
        {
            var (options, loggerFactory) = GetDefaults(logLevel);
            return new TcpClientIo<T, TR>(ipAddress ?? IpAddress, 10000, options, loggerFactory.CreateLogger<TcpClientIo<T, TR>>());
        }

        [OneTimeSetUp]
        public void Load()
        {
            Mocks = JsonExt.Deserialize<List<Mock>>(File.ReadAllText("MOCK_DATA_1000")).ToImmutableList();
        }

        [Test]
        public async Task SingleSendReceiveTest()
        {
            var tcpClient = GetClient<Mock>();
            var request = Mock.Create(1337);
            await tcpClient.SendAsync(request);
            var batch = await tcpClient.ReceiveAsync(1337L);
            var response = batch.First();
            Assert.IsTrue(request.Equals(response));
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            await tcpClient.DisposeAsync();
#else
            tcpClient.Dispose();
#endif
        }

        [Test]
        public async Task SingleByteAndByteArrayTest()
        {
            var tcpClient = GetClient<MockByteBody>();

            var mock = new MockByteBody
            {
                Id = 1,
                Body = "TestHello",
                TestByte = 123,
                TestByteArray = new byte[] {123, 124}
            };

            await tcpClient.SendAsync(mock);
            var batch = await tcpClient.ReceiveAsync(1);

#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            await tcpClient.DisposeAsync();
#else
            tcpClient.Dispose();
#endif
        }

        [TestCase(1000, 1, 5)]
        [TestCase(1000, 4, 5)]
        public void MultipleConsumersAsyncTest(int requests, int consumers, double timeout)
        {
            var requestsPerConsumer = requests / consumers;
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(timeout));
            var consumersList = Enumerable.Range(0, consumers).Select(i => GetClient<Mock>()).ToList();
            var requestQueue = 0;
            var waitersQueue = 0;
            var bytesWrite = 0L;
            var bytesRead = 0L;
            var sended = 0;
            var received = 0;

            Task.WaitAll(consumersList.Select(io => Task.Run(() => DoWork(io), cts.Token)).ToArray());

            void DoWork(TcpClientIo<Mock, Mock> tcpClient)
            {
                try
                {
                    var list = new List<Task>();
                    list.AddRange(Enumerable.Range(0, requestsPerConsumer).Select(i => (long) i).Select(SendAsync));
                    list.AddRange(Enumerable.Range(0, requestsPerConsumer).Select(i => (long) i).Select(ReceiveAsync));

                    Task.WaitAll(list.ToArray());

                    async Task SendAsync(long id)
                    {
                        var mock = Mock.Create(id);
                        await tcpClient.SendAsync(mock, cts.Token);
                        Interlocked.Increment(ref sended);
                    }

                    async Task ReceiveAsync(long id)
                    {
                        var batch = await tcpClient.ReceiveAsync(id, cts.Token);
                        var mock = batch.First();
                        Assert.IsTrue(mock.Size == mock.Data.Length);
                        Interlocked.Increment(ref received);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    Interlocked.Add(ref bytesWrite, (long) tcpClient.BytesWrite);
                    Interlocked.Add(ref bytesRead, (long) tcpClient.BytesRead);
                    Interlocked.Add(ref requestQueue, tcpClient.Requests);
                    Interlocked.Add(ref waitersQueue, tcpClient.Waiters);
                }
            }

            TestContext.WriteLine($"Requests: {requestQueue.ToString()}");
            TestContext.WriteLine($"Waiters: {waitersQueue.ToString()}");
            TestContext.WriteLine($"Sended: {sended.ToString()}");
            TestContext.WriteLine($"Received: {received.ToString()}");
            TestContext.WriteLine($"BytesWrite: {Math.Round(bytesWrite / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
            TestContext.WriteLine($"BytesRead: {Math.Round(bytesRead / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        [TestCase(1000, true)]
        [TestCase(1000, false)]
        public async Task ConsumingAsyncEnumerableTest(int requests, bool expandBatch)
        {
            var sended = 0;
            var received = 0;
            var cts = new CancellationTokenSource();
            TcpClientIo tcpClient = GetClient<Mock>();

            _ = Enumerable.Range(0, requests).Select(async i =>
            {
                var mock = Mock.Create(expandBatch ? 0 : i);
                await tcpClient.SendAsync(mock, cts.Token);
                Interlocked.Increment(ref sended);
            }).ToArray();

            _ = Task.Run(async () =>
            {
                while (received < requests && !cts.IsCancellationRequested)
                {
                    await Task.Delay(1, cts.Token);
                }

                cts.Cancel();
            }, cts.Token);

            try
            {
                if (expandBatch)
                {
                    await foreach (Mock _ in tcpClient.GetExpandableConsumingAsyncEnumerable(cts.Token))
                    {
                        Interlocked.Increment(ref received);
                    }
                }
                else
                {
                    await foreach (ITcpBatch<Mock> _ in tcpClient.GetConsumingAsyncEnumerable(cts.Token))
                    {
                        Interlocked.Increment(ref received);
                    }
                }
            }
            catch (TaskCanceledException) {}
            catch (Exception e)
            {
                TestContext.WriteLine(e);
            }
            finally
            {
                TestContext.WriteLine($"Requests: {tcpClient.Requests.ToString()}");
                TestContext.WriteLine($"Waiters: {tcpClient.Waiters.ToString()}");
                TestContext.WriteLine($"Sended: {sended.ToString()}");
                TestContext.WriteLine($"Received: {received.ToString()}");
                TestContext.WriteLine($"BytesWrite: {Math.Round(tcpClient.BytesWrite / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
                TestContext.WriteLine($"BytesRead: {Math.Round(tcpClient.BytesRead / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
            }
        }
#endif
        [Test]
        public async Task NoIdTest()
        {
            var client = GetClient<MockNoId>();
            var mock = new MockNoId
            {
                Body = "Qwerty!"
            };
            await client.SendAsync(mock);
            var batch = await client.ReceiveAsync(TcpClientIo.Unassigned);
            var response = batch.First();
        }

        [Test]
        public async Task SameIdTest()
        {
            const int requests = 500;
            var list = new List<int>();
            var count = 0;
            var error = 0;

            var tcpClient = GetClient<Mock>();

            _ = Task.Run(() => Parallel.For(0, requests, i =>
            {
                var mock = Mock.Create(0);

                try
                {
                    tcpClient.SendAsync(mock).GetAwaiter().GetResult();
                }
                catch
                {
                    Interlocked.Increment(ref error);
                }
            }));

            while (count < requests)
            {
                var delay = TestContext.CurrentContext.Random.Next(1, 200);
                await Task.Delay(delay);

                if (error > 0)
                    throw new Exception("Parallel.For has errors");

                var packageResult = await tcpClient.ReceiveAsync((long) 0);
                Assert.NotNull(packageResult);
                var queue = packageResult.Count;
                count += queue;

                list.AddRange(packageResult.Select(mock => mock.Size));

                TestContext.WriteLine($"({count.ToString()}/{requests.ToString()}) +{queue.ToString()}, by {delay.ToString()} ms, SendQueue: {tcpClient.Requests.ToString()}, ReadCount: {tcpClient.Waiters.ToString()}");
            }

            var havingCount = list.GroupBy(u => u).Where(p => p.Count() > 1).Select(ig => ig.Key.ToString()).Aggregate((acc, next) => $"{acc}, {next}");
            TestContext.WriteLine($"Non-UNIQ Sizes: {havingCount}");
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            await tcpClient.DisposeAsync();
#else
            tcpClient.Dispose();
#endif
        }

        [Test]
        public async Task DisposeTest()
        {
            var tcpClient = GetClient<Mock>();
            var timer = new System.Timers.Timer {Interval = 3000};
            timer.Start();
            timer.Elapsed += (sender, args) =>
            {
                ((System.Timers.Timer) sender).Stop();
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
                tcpClient.DisposeAsync().GetAwaiter().GetResult();
#else
                tcpClient.Dispose();
#endif
            };
            var mock = Mocks[666];
            while (true)
            {
                try
                {
                    await tcpClient.SendAsync(mock);
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

            timer.Dispose();
        }

        [Test]
        public async Task CancelSendReceiveTest()
        {
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            await using var tcpClient = GetClient<Mock>();
#else
            var tcpClient = GetClient<Mock>();
#endif
            var mock = Mocks[666];
            var attempts = 0;
            while (attempts < 3)
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                while (true)
                {
                    try
                    {
                        await tcpClient.SendAsync(mock, cts.Token);
                        await tcpClient.ReceiveAsync(mock.Id, cts.Token);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Got Exception: {e.GetType()}: {e}");
                        Assert.That(e.GetType() == typeof(OperationCanceledException) || e.GetType() == typeof(TaskCanceledException));
                        attempts++;
                        break;
                    }
                }
            }
#if !NETSTANDARD2_1 && !NETCOREAPP3_1 && !NETCOREAPP3_0
            tcpClient.Dispose();
#endif
        }
    }
}