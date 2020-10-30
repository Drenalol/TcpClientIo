using System.Collections.Generic;

namespace Drenalol.TcpClientIo.Batches
{
    /// <summary>
    /// Batch of responses.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <returns><see cref="ITcpBatch{TResponse}"/></returns>
    public interface ITcpBatch<TResponse> : IEnumerable<TResponse>
    {
        int Count { get; }
        void Add(TResponse response);
    }
}