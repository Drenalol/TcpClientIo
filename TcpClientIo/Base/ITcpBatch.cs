using System.Collections.Generic;
using Drenalol.Client;

namespace Drenalol.Base
{
    /// <summary>
    /// Batch of responses from <see cref="TcpClientIo{TRequest,TResponse}"/> of the specified Id.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ITcpBatch{T}"/></returns>
    public interface ITcpBatch<out T> : IEnumerable<T>
    {
        object Id { get; }
        int Count { get; }
    }
}