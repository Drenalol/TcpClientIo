using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo
{
    /// <summary>
    /// <inheritdoc cref="TcpClientIo{TRequest,TResponse}"/>
    /// </summary>
    /// <typeparam name="TRequestResponse"></typeparam>
    [DebuggerDisplay("Id: {Id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public class TcpClientIo<TRequestResponse> : TcpClientIo<TRequestResponse, TRequestResponse> where TRequestResponse : new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TRequestResponse}"/> class and connects to the specified port on the specified host.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TRequestResponse>> logger = null) : base(address, port, tcpClientIoOptions, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TRequestResponse}"/> class.
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TRequestResponse>> logger = null) : base(tcpClient, tcpClientIoOptions, logger)
        {
        }
    }
}