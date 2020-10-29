namespace Drenalol.TcpClientIo.Emulator
{
    public class ListenerEmulatorConfig
    {
        public int Port { get; set; }
        public bool ReaderMemoryPool { get; set; }
        public int ReaderBufferSize { get; set; }
        public int ReaderMinimumReadSize { get; set; }
        public bool WriterMemoryPool { get; set; }
        public int WriterBufferSize { get; set; }
    }
}