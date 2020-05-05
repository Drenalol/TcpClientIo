using System.IO.Pipelines;

namespace Drenalol.Base
{
    public sealed class ConcurrentTcpClientOptions
    {
        public StreamPipeReaderOptions StreamPipeReaderOptions { get; set; }
        public StreamPipeWriterOptions StreamPipeWriterOptions { get; set; }
        
        public static ConcurrentTcpClientOptions Default => new ConcurrentTcpClientOptions
        {
            StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 65536),
            StreamPipeWriterOptions = new StreamPipeWriterOptions()
        };
    }
}