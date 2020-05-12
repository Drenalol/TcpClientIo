using System.IO.Pipelines;

namespace Drenalol.Client
{
    public sealed class TcpClientIoOptions
    {
        public StreamPipeReaderOptions StreamPipeReaderOptions { get; set; }
        public StreamPipeWriterOptions StreamPipeWriterOptions { get; set; }
        
        public static TcpClientIoOptions Default => new TcpClientIoOptions
        {
            StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 65536),
            StreamPipeWriterOptions = new StreamPipeWriterOptions()
        };
    }
}