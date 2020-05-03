using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Base;
using Drenalol.Extensions;
using Drenalol.Models;
using NUnit.Framework;
using Timer = System.Timers.Timer;

namespace Drenalol
{
    public class ConcurrentTcpClientTests
    {
        private static readonly List<Mock> Mocks = JsonExt.Deserialize<List<Mock>>(File.ReadAllText("MOCK_DATA"));

        public static async Task<TcpPackage> ReadFactory(PipeReader reader, CancellationToken cancellationToken)
        {
            try
            {
                var packageId = (await reader.ReadExactlyAsync(4, cancellationToken)).AsUint32();
                var packageSize = (await reader.ReadExactlyAsync(4, cancellationToken)).AsUint32();
                var packageBody = await reader.ReadExactlyAsync(packageSize, cancellationToken);
                return new TcpPackage(packageId, packageBody);
            }
            catch
            {
                return null;
            }
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
        public async Task ParallelTest(int requests, int degree)
        {
            var tcpClient = new ConcurrentTcpClient(IPAddress.Any, 10000, ReadFactory);
            var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = degree};
            var sendMs = new ConcurrentBag<long>();
            var receiveMs = new ConcurrentBag<long>();
            var sended = 0;
            var received = 0;


            var writeTask = Task.Run(() => Parallel.For(0, requests, parallelOptions, Send));
            var readTask = Task.Run(() => Parallel.For(0, requests, parallelOptions, id => ReadAsync(id).GetAwaiter().GetResult()));
            await Task.WhenAll(writeTask, readTask);

            void Send(int id)
            {
                var sw = Stopwatch.StartNew();
                var mock = Mocks[TestContext.CurrentContext.Random.Next(Mocks.Count)];
                var package = new TcpPackage((uint) id, mock);
                tcpClient.Send(package.ToArray(), CancellationToken.None);
                sw.Stop();
                Interlocked.Increment(ref sended);
                sendMs.Add(sw.ElapsedMilliseconds);
            }

            async Task ReadAsync(int id)
            {
                var sw = Stopwatch.StartNew();
                var package = await tcpClient.ReceiveAsync((uint) id);
                
                if (package != null)
                {
                    sw.Stop();
                    Interlocked.Increment(ref received);
                }

                receiveMs.Add(sw.ElapsedMilliseconds);
            }

            TestContext.WriteLine($"Send Min Avg Max ms: {sendMs.Min().ToString()} {sendMs.Average().ToString(CultureInfo.CurrentCulture)} {sendMs.Max().ToString()}");
            TestContext.WriteLine($"Receive Min Avg Max ms: {receiveMs.Min().ToString()} {receiveMs.Average().ToString(CultureInfo.CurrentCulture)} {receiveMs.Max().ToString()}");
            TestContext.WriteLine($"SendQueue: {tcpClient.SendQueue.ToString()}");
            TestContext.WriteLine($"ReadCount: {tcpClient.ReadCount.ToString()}");
            TestContext.WriteLine($"Sended: {sended.ToString()}");
            TestContext.WriteLine($"Received: {received.ToString()}");
            tcpClient.Dispose();
        }

        [Test]
        public async Task SameIdTest()
        {
            const int requests = 500;
            var list = new List<uint>();
            var count = 0;
            var tcpClient = new ConcurrentTcpClient(IPAddress.Any, 10000, ReadFactory);

            _ = Task.Run(() => Parallel.For(0, requests, i => Assert.IsTrue(tcpClient.TrySend(new TcpPackage(1337, Mocks[TestContext.CurrentContext.Random.Next(Mocks.Count)]).ToArray()))));

            while (count < requests)
            {
                var delay = TestContext.CurrentContext.Random.Next(1, 200);
                await Task.Delay(delay);
                var packageResult = await tcpClient.ReceiveAsync(1337);
                Assert.NotNull(packageResult);
                var queue = packageResult.QueueCount;
                count += queue;

                while (packageResult.TryDequeue(out var package))
                {
                    list.Add(package.PackageSize);
                }

                TestContext.WriteLine($"({count.ToString()}/{requests.ToString()}) +{queue.ToString()}, by {delay.ToString()} ms, SendQueue: {tcpClient.SendQueue.ToString()}, ReadCount: {tcpClient.ReadCount.ToString()}");
            }

            var havingCount = list.GroupBy(u => u).Where(p => p.Count() > 1).Select(ig => ig.Key.ToString()).Aggregate((prev, next) => $"{prev}, {next}");
            TestContext.WriteLine($"Non-UNIQ Sizes: {havingCount}");
            tcpClient.Dispose();
        }

        [Test]
        public async Task DisposeTest()
        {
            var tcpClient = new ConcurrentTcpClient(IPAddress.Any, 10000, ReadFactory);
            using var timer = new Timer {Interval = 3000};
            timer.Start();
            timer.Elapsed += (sender, args) =>
            {
                ((Timer) sender).Stop();
                tcpClient.Dispose();
            };
            var mock = Mocks[666];
            while (true)
            {
                try
                {
                    tcpClient.TrySend(new TcpPackage(1337, mock).ToArray());
                    await tcpClient.ReceiveAsync(1337);
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
            using var tcpClient = new ConcurrentTcpClient(IPAddress.Any, 10000, ReadFactory);
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
                        tcpClient.Send(new TcpPackage(1337, mock).ToArray(), cts.Token);
                        await tcpClient.ReceiveAsync(1337, cts.Token);
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
                Hash = "5907081e0aa3851f7ecf497b783d528c",
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
    }
}