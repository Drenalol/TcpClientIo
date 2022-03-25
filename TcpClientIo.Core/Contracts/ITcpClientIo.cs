using System;
using System.IO.Pipelines;

namespace Drenalol.TcpClientIo.Contracts
{
    public interface ITcpClientIo : IDisposable, IAsyncDisposable, IDuplexPipe
    {
        long BytesWrite { get; }
        long BytesRead { get; }
        int Waiters { get; }
        int Requests { get; }
    }
}