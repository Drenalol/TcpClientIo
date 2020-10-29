using System.Threading;
using NUnit.Framework;

namespace Drenalol.TcpClientIo.Emulator
{
    public class UseTcpListenerTest
    {
        private CancellationTokenSource _cts;
        
        [OneTimeSetUp]
        public void Ctor()
        {
            _cts = new CancellationTokenSource();
            
            ListenerEmulator.Create(_cts.Token, ListenerEmulatorConfig.Default);
        }

        [OneTimeTearDown]
        public void Dctor()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}