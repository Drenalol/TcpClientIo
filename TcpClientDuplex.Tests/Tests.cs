using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TcpClientDuplex.Base;
using TcpClientDuplex.Extensions;
using TcpClientDuplex.Models;

namespace TcpClientDuplex.Tests
{
    public class Tests
    {
        private static readonly Random Rnd = new Random();
        private static readonly List<Mock> Mocks = JsonExt.Deserialize<List<Mock>>(File.ReadAllText("MOCK_DATA"));

        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(100000)]
        [TestCase(500000)]
        public void ParallelTcpClientTest(int requests)
        {
            var tcpDuplex = new Base.TcpClientDuplex(IPAddress.Any, 10000, PipeReaderExt.ReadFactory);
            var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = 4};
            var sended = 0;
            var received = 0;

            Parallel.For(0, requests, parallelOptions, id =>
            {
                if (tcpDuplex.TrySend(new TcpPackage((uint) id, Mocks[Rnd.Next(Mocks.Count)].ToString()).ToArray()))
                    Interlocked.Increment(ref sended);
            });

            Parallel.For(0, requests, parallelOptions, id =>
            {
                var sw = Stopwatch.StartNew();
                if (tcpDuplex.Receive((uint) id) != null)
                    Interlocked.Increment(ref received);
                sw.Stop();
            });

            TestContext.WriteLine($"ToWrite: {tcpDuplex.ToWrite.ToString()}");
            TestContext.WriteLine($"Queue: {tcpDuplex.Queue.ToString()}");
            TestContext.WriteLine($"Sended: {sended.ToString()}");
            TestContext.WriteLine($"Received: {received.ToString()}");
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