#if NETSTANDARD2_1
using System.Collections.Generic;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Abstractions
{
    public abstract class TcpClientIo
    {
        public static readonly object Unassigned = new object();
        public abstract Task<bool> SendAsync(object request, CancellationToken token = default);
        public abstract Task<object> ReceiveAsync(object responseId, CancellationToken token = default, bool skipMe = true);
#if NETSTANDARD2_1
        public abstract IAsyncEnumerable<object> GetConsumingAsyncEnumerable(CancellationToken token = default, bool skipMe = true);
        public abstract IAsyncEnumerable<object> GetExpandableConsumingAsyncEnumerable(CancellationToken token = default, bool skipMe = true);
        public abstract ValueTask DisposeAsync();
#else
        public abstract void Dispose();
#endif
    }
}