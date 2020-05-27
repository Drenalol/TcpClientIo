using System.Collections.Generic;

namespace Drenalol.Base
{
    public interface ITcpBatch<out T> : IEnumerable<T>
    {
        object Id { get; }
        int Count { get; }
    }
}