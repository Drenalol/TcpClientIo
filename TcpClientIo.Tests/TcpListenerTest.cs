using System.Threading;
using Drenalol.TcpClientIo.Emulator;
using NUnit.Framework;

namespace Drenalol.TcpClientIo
{
    public class TcpListenerTest
    {
        private CancellationTokenSource _cts;
        
        [OneTimeSetUp]
        public void Ctor()
        {
            _cts = new CancellationTokenSource();
            
            var config = new ListenerEmulatorConfig
            {
                Port = 10000,
                ReaderMemoryPool = false,
                WriterMemoryPool = false,
                ReaderBufferSize = -1,
                ReaderMinimumReadSize = -1,
                WriterBufferSize = -1
            };
            
            ListenerEmulator.Create(_cts.Token, config);
        }

        [OneTimeTearDown]
        public void Dctor()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}