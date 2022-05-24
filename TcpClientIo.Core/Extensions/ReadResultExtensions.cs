using System.Buffers;
using System.IO.Pipelines;

namespace Drenalol.TcpClientIo.Extensions
{
    internal static class ReadResultExtensions
    {
        public static ReadOnlySequence<byte> Slice(in this ReadResult readResult, int length, long start = 0) => readResult.Buffer.Slice(start, length);
    }
}