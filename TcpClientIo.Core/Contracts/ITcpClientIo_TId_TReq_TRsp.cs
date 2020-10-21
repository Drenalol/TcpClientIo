
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Batches;
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
#endif

namespace Drenalol.TcpClientIo.Contracts
{
    public interface ITcpClientIo<TId, in TRequest, TResponse> : ITcpClientIo
    {
        ImmutableDictionary<TId, ITcpBatch<TId, TResponse>> GetWaiters();
        Task<bool> SendAsync(TRequest request, CancellationToken token = default);
        Task<ITcpBatch<TId, TResponse>> ReceiveAsync(TId responseId, CancellationToken token = default);
#if NETSTANDARD2_1
        IAsyncEnumerable<ITcpBatch<TId, TResponse>> GetConsumingAsyncEnumerable(CancellationToken token = default);
        IAsyncEnumerable<TResponse> GetExpandableConsumingAsyncEnumerable(CancellationToken token = default);
#endif
    }
}