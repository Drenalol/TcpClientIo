using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_1
#endif

namespace Drenalol.TcpClientIo
{
    public abstract class TcpClientIoBase
    {
        public static readonly object Unassigned = new object();

        public abstract ulong BytesWrite { get; set; }
        public abstract ulong BytesRead { get; set; }

        public abstract int Waiters { get; }
        public abstract int Requests { get; }

        public abstract ImmutableDictionary<object, object> GetWaiters(bool skipMe = true);

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