using System.Collections;
using System.Collections.Generic;

namespace Drenalol.TcpClientIo.Batches
{
    /// <summary>
    /// Default TcpBatch instance
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public sealed class DefaultTcpBatch<TResponse> : ITcpBatch<TResponse>
    {
        private readonly IList<TResponse> _internalList;
        public int Count => _internalList.Count;

        public DefaultTcpBatch()
        {
            _internalList = new List<TResponse>();
        }

        public void Add(TResponse response) => _internalList.Add(response);
        
        public IEnumerator<TResponse> GetEnumerator() => _internalList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}