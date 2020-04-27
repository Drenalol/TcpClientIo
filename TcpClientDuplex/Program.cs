using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TcpClientDuplex.Extensions;
using TcpClientDuplex.Models;

namespace TcpClientDuplex
{
    public class Program
    {
        private static CancellationTokenSource _stop;
        public static readonly Random Rnd = new Random();
        private static List<Mock> _mocks;
        
        public static async Task Main()
        {
            _stop = new CancellationTokenSource();
            _mocks = JsonExt.Deserialize<List<Mock>>(await File.ReadAllTextAsync("MOCK_DATA"));

            Console.CancelKeyPress += (sender, args) => _stop.Cancel();

            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(100);
            }
        }
    }
}