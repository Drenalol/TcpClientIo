using BenchmarkDotNet.Running;

namespace TcpClientIo.Benchmarks
{
    internal static class Program
    {
        private static void Main()
        {
            BenchmarkRunner.Run<TcpSerializerBenchmark>();
        }
    }
}