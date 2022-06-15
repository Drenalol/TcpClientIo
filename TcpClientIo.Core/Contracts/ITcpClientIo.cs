using System;
using System.IO.Pipelines;

namespace Drenalol.TcpClientIo.Contracts
{
    public interface ITcpClientIo : IAsyncDisposable, IDuplexPipe
    {
        long BytesWrite { get; }
        long BytesRead { get; }
        int Waiters { get; }
        int Requests { get; }
        bool IsBroken { get; }
    }
}