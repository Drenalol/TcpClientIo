using System;
using System.IO.Pipelines;

namespace Drenalol.TcpClientIo
{
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
    public interface ITcpClientIo : IAsyncDisposable, IDuplexPipe
#else
    public interface ITcpClientIo : IDisposable, IDuplexPipe
#endif
    {
        ulong BytesWrite { get; }
        ulong BytesRead { get; }
        int Waiters { get; }
        int Requests { get; }
    }
}