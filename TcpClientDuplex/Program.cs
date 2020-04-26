using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TcpClientDuplex
{
    public class Program
    {
        private static CancellationTokenSource _stop;
        private static readonly Random Rnd = new Random();
        private static List<TcpPackage<uint, Mock>> _packages;

        /*private static async Task Do()
        {
            var listener = new ListenerEmulator(10000, _stop.Token);
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Any, 10000);
            var stream = client.GetStream();
            var r = PipeReader.Create(stream);
            var end = 0;

            while (true)
            {
                await stream.WriteAsync(new ReadOnlyMemory<byte>(Q()));
                Task.Run(async () =>
                {
                    await Task.Delay(10000);
                    await stream.WriteAsync(new ReadOnlyMemory<byte>(Q()));
                });
                var result = await r.ReadExactlyAsync(0, 4);
                Console.WriteLine(result.Select(b => b.ToString()).Aggregate((i, j) => $"{i},{j}"));

                byte[] Q()
                {
                    var maxBytes = Rnd.Next(3, 6);
                    var data = Enumerable.Range(end, maxBytes).Select(Convert.ToByte).ToArray();
                    end = data.Last() + 1;
                    return data;
                }
            }
        }*/

        public static async Task Main()
        {
            const int port = 10000;
            _stop = new CancellationTokenSource();
            ListenerEmulator.Create(port, _stop.Token);
            var mockData = await File.ReadAllLinesAsync("MOCK_DATA");
            _packages = mockData.Select(json => new TcpPackage<uint, Mock>(0U, json)).ToList();
            var index = 0U;
            foreach (var package in _packages)
            {
                package.PackageId = ++index;
            }

            Console.CancelKeyPress += (sender, args) => _stop.Cancel();
        }

        public static async Task<ITcpPackage<uint, Mock>> ReadFactory(PipeReader reader, CancellationToken cancellationToken)
        {
            var packageId = (await reader.ReadExactlyAsync(0, 4, cancellationToken)).AsUint32();
            var packageSize = (await reader.ReadExactlyAsync(4, 4, cancellationToken)).AsUint32();
            var packageBody = (await reader.ReadExactlyAsync(8, packageSize, cancellationToken)).AsAsciiString();
            return new TcpPackage<uint, Mock>(packageId, packageBody);
        }
    }
}