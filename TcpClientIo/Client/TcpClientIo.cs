using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Drenalol.Client
{
    [DebuggerDisplay("Id: {_id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public class TcpClientIo<TRequestResponse> : TcpClientIo<TRequestResponse, TRequestResponse> where TRequestResponse : new()
    {
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null) : base(address, port, tcpClientIoOptions)
        {
            
        }

        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null) : base(tcpClient, tcpClientIoOptions)
        {
            
        }
    }
}