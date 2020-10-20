using System.Collections.Generic;

namespace Drenalol.TcpClientIo
{
    /// <summary>
    /// Batch of responses of the specified Id.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <returns><see cref="ITcpBatch{TId, TResponse}"/></returns>
    public interface ITcpBatch<out TId, TResponse> : IEnumerable<TResponse>
    {
        TId Id { get; }
        int Count { get; }

        void Update(TResponse response);
    }
}