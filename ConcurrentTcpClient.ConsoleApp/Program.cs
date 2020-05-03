using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentTcpClient.Base;
using ConcurrentTcpClient.Extensions;
using ConcurrentTcpClient.Models;
using ConcurrentTcpClient.Tests;

namespace ConcurrentTcpClient.ConsoleApp
{
    static class Program
    {
        static async Task Main()
        {
            const int instances = 4;
            const int requests = 1000;
            var random = new Random();
            var mocks = JsonExt.Deserialize<List<Mock>>(await File.ReadAllTextAsync("MOCK_DATA")).ToImmutableList();
            var cts = new CancellationTokenSource();
            var options = new ParallelOptions {MaxDegreeOfParallelism = -1};
            var tcpOptions = new TcpClientAsyncOptions {StreamPipeReaderOptions = new StreamPipeReaderOptions()};
            Console.CancelKeyPress += (sender, args) => cts.Cancel();

            while (!cts.IsCancellationRequested)
            {
                Parallel.For(0, instances, options, instanceId =>
                {
                    var tcpDuplex = new TcpClient(IPAddress.Any, 10001, TcpClientTests.ReadFactory, tcpOptions);
                    var write = Task.Run(() => Parallel.For(0, requests, options, writeId =>
                    {
                        var mock = mocks[random.Next(mocks.Count)];
                        var bytes = mock.GetBytes();
                        tcpDuplex.Send(new TcpPackage((uint) writeId, bytes).ToArray());
                    }), cts.Token);
                    var read = Task.Run(() => Parallel.For(0, requests, options, readId =>
                    {
                        var package = tcpDuplex.TryReceiveAsync((uint) readId).Result;
                        Console.Out.WriteLine($"{DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} Instance: {instanceId.ToString()} Package: {readId.ToString()}:{package.PackageId.ToString()} received {package.PackageSize.ToString()} bytes");
                    }), cts.Token);
                    Task.WaitAll(write, read);
                    tcpDuplex.DisposeAsync().GetAwaiter().GetResult();
                });
            }
        }
    }
}