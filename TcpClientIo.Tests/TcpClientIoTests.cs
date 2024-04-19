using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Client;
using Drenalol.TcpClientIo.Contracts;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Emulator;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Stuff;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using NUnit.Framework;

namespace Drenalol.TcpClientIo
{
    [TestFixture(TestOf = typeof(TcpClientIo<,>))]
    public class TcpClientIoTests : UseTcpListenerTest
    {
        public static readonly IPAddress IpAddress = IPAddress.Any;

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
            options.PipeExecutorOptions = PipeExecutor.Logging;
            
            var loggerFactory = LoggerFactory.Create(lb =>
            {
                //lb.AddFilter("Drenalol.Client.TcpClientIo.Core", logLevel);
                lb.SetMinimumLevel(logLevel);
                lb.AddDebug();
                lb.AddConsole();
            });

            return (options, loggerFactory);
        }

        public static TcpClientIo<TId, T, TR> GetClient<TId, T, TR>(IPAddress ipAddress = null, int port = 10000, LogLevel logLevel = LogLevel.Warning) where TR : new() where TId : struct
        {
            var (options, loggerFactory) = GetDefaults(logLevel);
            return new TcpClientIo<TId, T, TR>(ipAddress ?? IpAddress, port, options, loggerFactory.CreateLogger<TcpClientIo<T, TR>>());
        }

        public static TcpClientIo<T, TR> GetClient<T, TR>(IPAddress ipAddress = null, int port = 10000, LogLevel logLevel = LogLevel.Warning) where TR : new()
        {
            var (options, loggerFactory) = GetDefaults(logLevel);
            return new TcpClientIo<T, TR>(ipAddress ?? IpAddress, port, options, loggerFactory.CreateLogger<TcpClientIo<T, TR>>());
        }

        [Test]
        public async Task SingleSendReceiveTest()
        {
            var tcpClient = GetClient<long, Mock, Mock>(logLevel: LogLevel.Debug);
            var request = Mock.Default();
            await tcpClient.SendAsync(request);
            var batch = await tcpClient.ReceiveAsync(1337L);
            var response = batch.First();
            Assert.IsTrue(request.Equals(response));
            await tcpClient.DisposeAsync();
            Assert.True(tcpClient.IsBroken);
        }

        [Test]
        public async Task SingleByteAndByteArrayTest()
        {
            var tcpClient = GetClient<int, MockByteBody, MockByteBody>();

            var mock = new MockByteBody
            {
                Id = 1,
                Body = "TestHello",
                TestByte = 123,
                TestByteArray = new byte[] {123, 124}
            };

            await tcpClient.SendAsync(mock);
            var batch = await tcpClient.ReceiveAsync(1);

            await tcpClient.DisposeAsync();
            Assert.True(tcpClient.IsBroken);
        }
        
        [Test]
        public async Task MockBodyInSequenceTest()
        {
            var tcpClient = GetClient<int, MockMemoryBody, MockMemoryBody>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await Parallel.ForEachAsync(Enumerable.Range(1, 1000), cts.Token, SendAsync);
            cts.Dispose();
            await tcpClient.DisposeAsync();
            Assert.True(tcpClient.IsBroken);
            Assert.AreEqual(tcpClient.BytesRead, tcpClient.BytesWrite);
            Assert.False(cts.IsCancellationRequested);
            
            async ValueTask SendAsync(int id, CancellationToken cancellationToken)
            {
                var bytes = new byte[TestContext.CurrentContext.Random.Next(1024 * 32)];
                TestContext.CurrentContext.Random.NextBytes(bytes);
                var mock = new MockMemoryBody
                {
                    Id = id,
                    TestByte = 123,
                    TestByteArray = new byte[] { 111, 222 },
                    Body = bytes.ToSequence()
                };

                await tcpClient.SendAsync(mock, cancellationToken);
                var response = (await tcpClient.ReceiveAsync(id, cancellationToken)).Single();
                
                try
                {
                    Assert.True(mock == response);
                }
                catch
                {
                    // ReSharper disable once AccessToDisposedClosure
                    cts.Cancel();
                }
            }
        }

        [TestCase(1000, 1, 5)]
        [TestCase(1000, 4, 5)]
        public void MultipleConsumersAsyncTest(int requests, int consumers, double timeout)
        {
            var requestsPerConsumer = requests / consumers;
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(timeout));
            var consumersList = Enumerable.Range(0, consumers).Select(i => GetClient<long, Mock, Mock>()).ToList();
            var requestQueue = 0;
            var waitersQueue = 0;
            var bytesWrite = 0L;
            var bytesRead = 0L;
            var sended = 0;
            var received = 0;

            Task.WaitAll(consumersList.Select(io => Task.Run(() => DoWork(io), cts.Token)).ToArray());

            void DoWork(ITcpClientIo<long, Mock, Mock> tcpClient)
            {
                try
                {
                    var list = new List<Task>();
                    list.AddRange(Enumerable.Range(0, requestsPerConsumer).Select(i => (long) i).Select(SendAsync));
                    list.AddRange(Enumerable.Range(0, requestsPerConsumer).Select(i => (long) i).Select(ReceiveAsync));

                    Task.WaitAll(list.ToArray());

                    async Task SendAsync(long id)
                    {
                        var mock = Mock.Default(id);
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
                    Interlocked.Add(ref bytesWrite, tcpClient.BytesWrite);
                    Interlocked.Add(ref bytesRead, tcpClient.BytesRead);
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

        [TestCase(1000, true)]
        [TestCase(1000, false)]
        public async Task ConsumingAsyncEnumerableTest(int requests, bool expandBatch)
        {
            var sended = 0;
            var received = 0;
            var cts = new CancellationTokenSource();
            ITcpClientIo<long, Mock, Mock> tcpClient = GetClient<long, Mock, Mock>();

            _ = Enumerable.Range(0, requests).Select(async i =>
            {
                var mock = Mock.Default(expandBatch ? 0 : i);
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
                    await foreach (var _ in tcpClient.GetExpandableConsumingAsyncEnumerable(cts.Token))
                    {
                        Interlocked.Increment(ref received);
                    }
                }
                else
                {
                    await foreach (var _ in tcpClient.GetConsumingAsyncEnumerable(cts.Token))
                    {
                        Interlocked.Increment(ref received);
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
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

        [Test]
        public async Task NoIdTest()
        {
            var client = GetClient<MockNoId, MockNoId>();
            var mock = new MockNoId
            {
                Body = "Qwerty!"
            };
            await client.SendAsync(mock);
            var batch = await client.ReceiveAsync();
            var mockNoId = batch.First();
            Assert.IsTrue(mock.Size == mockNoId.Size);
        }

        [Test]
        public async Task NoIdNoBodyTest()
        {
            var client = GetClient<MockOnlyMetaData, MockOnlyMetaData>();
            var mock = new MockOnlyMetaData
            {
                Test = 1337,
                Long = 777788889999
            };
            await client.SendAsync(mock);
            var batch = await client.ReceiveAsync();
            var mockNoId = batch.First();
            Assert.IsTrue(mock.Test == mockNoId.Test);
            Assert.IsTrue(mock.Long == mockNoId.Long);
        }

        [Test]
        public async Task SameIdTest()
        {
            const int requests = 10;
            var list = new List<int>();
            var count = 0;
            var error = 0;

            var tcpClient = GetClient<long, Mock, Mock>();

            _ = Task.Run(() => Parallel.For(0, requests, i =>
            {
                var mock = Mock.Default(0);

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

                var packageResult = await tcpClient.ReceiveAsync(0);
                Assert.NotNull(packageResult);
                var queue = packageResult.Count;
                count += queue;

                list.AddRange(packageResult.Select(mock => mock.Size));

                TestContext.WriteLine($"({count.ToString()}/{requests.ToString()}) +{queue.ToString()}, by {delay.ToString()} ms, SendQueue: {tcpClient.Requests.ToString()}, ReadCount: {tcpClient.Waiters.ToString()}");
            }

            var havingCount = list.GroupBy(u => u).Where(p => p.Count() > 1).Aggregate("", (acc, next) => $"{next.Key.ToString()}, {acc}");
            TestContext.WriteLine($"Non-UNIQ Sizes: {havingCount}");

            await tcpClient.DisposeAsync();
            Assert.True(tcpClient.IsBroken);
        }

        [Test]
        public async Task DisposeTest()
        {
            var tcpClient = GetClient<long, Mock, Mock>();
            var timer = new System.Timers.Timer {Interval = 3000};
            timer.Start();
            timer.Elapsed += (sender, _) =>
            {
                ((System.Timers.Timer) sender)?.Stop();
                tcpClient.DisposeAsync().GetAwaiter().GetResult();
            };
            var mock = Mock.Default();
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
                    Assert.That(exType == typeof(OperationCanceledException) || exType == typeof(TaskCanceledException) || exType == typeof(ObjectDisposedException));
                    Assert.True(tcpClient.IsBroken);
                    break;
                }
            }

            timer.Dispose();
        }

        [Test]
        public async Task CancelSendReceiveTest()
        {
            await using var tcpClient = GetClient<long, Mock, Mock>();
            var mock = Mock.Default();
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
                        Assert.False(tcpClient.IsBroken);
                        attempts++;
                        break;
                    }
                }
            }
        }

        [Test]
        public async Task EmptyBodyTest()
        {
            var mock = new MockNoIdEmptyBody {Length = 0, Empty = ""};
            var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>();
            await client.SendAsync(mock);
            Assert.NotNull(await client.ReceiveAsync(default));
        }

        [Test]
        public void ReceiveAndListenerDisconnectTest()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var cfg = ListenerEmulatorConfig.Default;
            cfg.Port = TestContext.CurrentContext.Random.Next(10001, 12001);
            ListenerEmulator.Create(cts.Token, cfg);
            var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: cfg.Port);

            Assert.CatchAsync<TcpClientIoException>(async () => await client.ReceiveAsync(0, CancellationToken.None));
            Assert.True(client.IsBroken);
        }
        
        [Test]
        public void ReceiveAndCancelTaskTest()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var cfg = ListenerEmulatorConfig.Default;
            cfg.Port = TestContext.CurrentContext.Random.Next(10001, 12001);
            ListenerEmulator.Create(cts.Token, cfg);
            var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: cfg.Port);

            Assert.CatchAsync<TaskCanceledException>(
                async () =>
                {
                    using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await client.ReceiveAsync(0, cts2.Token);
                }
            );
            Assert.False(client.IsBroken);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ConsumeAndDisconnectTest(bool ownToken)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var cfg = ListenerEmulatorConfig.Default;
            cfg.Port = 10001;
            ListenerEmulator.Create(cts.Token, cfg);
            var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: 10001);

            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                await foreach (var _ in client.GetExpandableConsumingAsyncEnumerable(ownToken ? cts2.Token : default))
                {
                }
            }
            catch (Exception e)
            {
                Assert.IsInstanceOf<TcpClientIoException>(e);
                Assert.True(client.IsBroken);
            }
        }

        [Test]
        public async Task ListenerDisconnectTest()
        {
            using var cts = new CancellationTokenSource();
            var cfg = ListenerEmulatorConfig.Default;
            cfg.Port = 10001;
            ListenerEmulator.Create(cts.Token, cfg);
            var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: 10001);

            cts.Cancel();
            await Task.Delay(5000, CancellationToken.None);
            Assert.CatchAsync<TcpClientIoException>(() => client.SendAsync(new MockNoIdEmptyBody(), CancellationToken.None));
            Assert.True(client.IsBroken);
        }
    }
}