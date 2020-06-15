using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Abstractions
{
    public abstract class TcpClientIo
    {
        public static readonly object Unassigned = new object();
        public abstract Task SendAsync(object request, CancellationToken token = default);
        public abstract Task<object> ReceiveAsync(object responseId, CancellationToken token = default, bool isObject = true);
#if NETSTANDARD2_1
        public abstract IAsyncEnumerable<object> GetConsumingAsyncEnumerable(CancellationToken token = default, bool isObject = true);
        public abstract IAsyncEnumerable<object> GetExpandableConsumingAsyncEnumerable(CancellationToken token = default, bool isObject = true);
#endif
    }
}