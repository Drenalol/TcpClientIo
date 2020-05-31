using System.Collections.Generic;

namespace Drenalol.Base
{
    /// <summary>
    /// Batch of responses of the specified Id.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ITcpBatch{T}"/></returns>
    public interface ITcpBatch<out T> : IEnumerable<T>
    {
        object Id { get; }
        int Count { get; }
    }
}