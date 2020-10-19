using System.Collections.Generic;

namespace TcpClientIo.TcpBatchRules
{
    /// <summary>
    /// Batch of responses of the specified Id.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ITcpBatch{T}"/></returns>
    public interface ITcpBatch<T> : IEnumerable<T>
    {
        object Id { get; }
        int Count { get; }

        void Update(T response);
    }
}