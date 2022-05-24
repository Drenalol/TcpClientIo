using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Batches;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Options;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Client
{
    /// <summary>
    /// Wrapper of TcpClient what help focus on WHAT you transfer over TCP, not HOW.
    /// <para>No Identifier version.</para>
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    [DebuggerDisplay("Id: {Id,nq}, Requests: {Requests,nq}, Waiters: {Waiters,nq}")]
    public class TcpClientIo<TInput, TOutput> : TcpClientIo<int, TInput, TOutput> where TOutput : new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TId, TRequestResponse}"/> class and connects to the specified port on the specified host.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions? tcpClientIoOptions = null, ILogger<TcpClientIo<TInput, TOutput>>? logger = null) : base(address, port, tcpClientIoOptions, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClientIo{TId, TRequestResponse}"/> class.
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="tcpClientIoOptions"></param>
        /// <param name="logger"></param>
        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions? tcpClientIoOptions = null, ILogger<TcpClientIo<TInput, TOutput>>? logger = null) : base(tcpClient, tcpClientIoOptions, logger)
        {
        }

        /// <summary>
        /// Begins an asynchronous request to receive any response from a connected <see cref="TcpClientIo{TRequest,TResponse}"/> object.
        /// </summary>
        /// <param name="token"></param>
        /// <returns><see cref="ITcpBatch{TResponse}"/></returns>
        /// <exception cref="TcpClientIoException"></exception>
        public Task<ITcpBatch<TOutput>> ReceiveAsync(CancellationToken token = default) => ReceiveAsync(default, token);
    }
}