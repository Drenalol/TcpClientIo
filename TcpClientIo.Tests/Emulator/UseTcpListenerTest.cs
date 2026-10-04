using NUnit.Framework;

namespace Drenalol.TcpClientIo.Emulator;

public class UseTcpListenerTest
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Port of the shared echo emulator started for the fixture.
    /// </summary>
    protected int EmulatorPort { get; private set; }

    [OneTimeSetUp]
    public void Ctor()
    {
        _cts = new CancellationTokenSource();

        var emulator = ListenerEmulator.Create(_cts.Token, ListenerEmulatorConfig.Default);
        EmulatorPort = emulator.Port;
    }

    [OneTimeTearDown]
    public void Dctor()
    {
        _cts!.Cancel();
        _cts.Dispose();
    }
}
