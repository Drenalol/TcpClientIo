using System.IO.Pipelines;

namespace Drenalol.Base
{
    public sealed class ConcurrentTcpClientOptions
    {
        public StreamPipeReaderOptions StreamPipeReaderOptions { get; set; }
        public StreamPipeWriterOptions StreamPipeWriterOptions { get; set; }
    }
}