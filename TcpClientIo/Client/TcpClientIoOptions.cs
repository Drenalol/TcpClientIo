using System.IO.Pipelines;

namespace Drenalol.Client
{
    public sealed class TcpClientIoOptions
    {
        public StreamPipeReaderOptions StreamPipeReaderOptions { get; set; }
        public StreamPipeWriterOptions StreamPipeWriterOptions { get; set; }
        public int TcpClientSendTimeout { get; set; }
        public int TcpClientReceiveTimeout { get; set; }
        
        public static TcpClientIoOptions Default => new TcpClientIoOptions
        {
            StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 65536),
            StreamPipeWriterOptions = new StreamPipeWriterOptions(),
            TcpClientSendTimeout = 60000,
            TcpClientReceiveTimeout = 60000
        };
    }
}