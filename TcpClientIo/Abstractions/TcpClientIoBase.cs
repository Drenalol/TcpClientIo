using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Abstractions
{
    public abstract class TcpClientIoBase
    {
        public abstract Task SendAsync(object request, CancellationToken token = default);
        public abstract Task<object> ReceiveAsync(object responseId, CancellationToken token = default, bool isObject = true);
    }
}