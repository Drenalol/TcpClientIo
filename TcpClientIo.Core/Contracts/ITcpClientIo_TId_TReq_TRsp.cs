using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Batches;

namespace Drenalol.TcpClientIo.Contracts
{
    public interface ITcpClientIo<TId, in TRequest, TResponse> : ITcpClientIo
    {
        ImmutableDictionary<TId, WaiterInfo<ITcpBatch<TResponse>>> GetWaiters();
        Task<bool> SendAsync(TRequest request, CancellationToken token = default);
        Task<ITcpBatch<TResponse>> ReceiveAsync(TId responseId, CancellationToken token = default);
        IAsyncEnumerable<ITcpBatch<TResponse>> GetConsumingAsyncEnumerable(CancellationToken token = default);
        IAsyncEnumerable<TResponse> GetExpandableConsumingAsyncEnumerable(CancellationToken token = default);
    }
}