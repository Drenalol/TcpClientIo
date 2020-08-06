using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Abstractions
{
    public abstract class TcpClientIo
    {
        public static readonly object Unassigned = new object();
        public abstract Task SendAsyncBase(object request, CancellationToken token = default);
        public abstract Task<object> ReceiveAsyncBase(object responseId, CancellationToken token = default);
#if NETSTANDARD2_1
        public abstract IAsyncEnumerable<object> GetConsumingAsyncEnumerableBase(CancellationToken token = default);
        public abstract IAsyncEnumerable<object> GetExpandableConsumingAsyncEnumerableBase(CancellationToken token = default);
#endif
        public abstract void DisposeBase();
    }
}