using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo
{
    /// <summary>
    /// Wrapper of TcpClient what help focus on WHAT you transfer over TCP, not HOW.
    /// <para>No Identifier version.</para>
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    [DebuggerDisplay("Id: {Id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public class TcpClientIo<TRequest, TResponse> : TcpClientIo<int, TRequest, TResponse> where TResponse : new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TId, TRequestResponse}"/> class and connects to the specified port on the specified host.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TRequest, TResponse>> logger = null) : base(address, port, tcpClientIoOptions, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TId, TRequestResponse}"/> class.
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null, ILogger<TcpClientIo<TRequest, TResponse>> logger = null) : base(tcpClient, tcpClientIoOptions, logger)
        {
        }
    }
}