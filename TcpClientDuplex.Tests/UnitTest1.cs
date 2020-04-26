using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace TcpClientDuplex.Tests
{
    public class Tests
    {
        private static CancellationTokenSource _stop;
        private static readonly Random Rnd = new Random();
        private static string[] _mockDatas;
        private static List<TcpPackage<uint, Mock>> _packages;
        
        [OneTimeSetUp]
        public async Task Setup()
        {
            const int port = 10000;
            _stop = new CancellationTokenSource();
            ListenerEmulator.Create(port, _stop.Token);
            _mockDatas = await File.ReadAllLinesAsync("MOCK_DATA");
            _packages = _mockDatas.Select(json => new TcpPackage<uint, Mock>(0U, json)).ToList();
            var index = 0U;
            foreach (var package in _packages)
            {
                package.PackageId = ++index;
            }
        }

        [Test]
        public void Test1()
        {
            var tcpDuplex = new TcpClientDuplex<uint, Mock>(IPAddress.Any, 10000, Program.ReadFactory);
            tcpDuplex.Send(_packages.First().ToArray());
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
                Hash = "5907081e0aa3851f7ecf497b783d528c"
            };
            var mockS = JsonConvert.SerializeObject(mock, TcpClientDuplexExt.JsonSerializerSettings);
            var mockD = JsonConvert.DeserializeObject<Mock>(mockS, TcpClientDuplexExt.JsonSerializerSettings);
            var mockS2 = JsonConvert.SerializeObject(mockD, TcpClientDuplexExt.JsonSerializerSettings);
            Assert.AreEqual(mockS2.Length, mockS.Length);
        }
    }
}