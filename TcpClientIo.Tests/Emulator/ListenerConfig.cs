namespace Drenalol.TcpClientIo.Emulator;

public sealed class ListenerEmulatorConfig
{
    /// <summary>
    /// Port to listen on. Use 0 to bind an ephemeral port and read it back from <see cref="ListenerEmulator.Port"/>.
    /// </summary>
    public int Port { get; set; }

    public bool ReaderMemoryPool { get; set; }
    public int ReaderBufferSize { get; set; }
    public int ReaderMinimumReadSize { get; set; }
    public bool WriterMemoryPool { get; set; }
    public int WriterBufferSize { get; set; }

    public static ListenerEmulatorConfig Default => new()
    {
        Port = 0,
        ReaderMemoryPool = false,
        WriterMemoryPool = false,
        ReaderBufferSize = -1,
        ReaderMinimumReadSize = -1,
        WriterBufferSize = -1
    };
}
