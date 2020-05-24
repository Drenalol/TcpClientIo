using System.Diagnostics;
using System.Net;

namespace Drenalol.Client
{
    [DebuggerDisplay("Id: {_id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public class TcpClientIo<TRequestResponse> : TcpClientIo<TRequestResponse, TRequestResponse> where TRequestResponse : new()
    {
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null) : base(address, port, tcpClientIoOptions)
        {
            
        }
    }
}