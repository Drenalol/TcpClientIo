using System.Collections;
using System.Collections.Generic;

namespace Drenalol.TcpClientIo.Batches
{
    /// <summary>
    /// Default TcpBatch instance
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public sealed class DefaultTcpBatch<TId, TResponse> : ITcpBatch<TId, TResponse>
    {
        private readonly IList<TResponse> _internalList;
        public TId Id { get; }
        public int Count => _internalList.Count;

        public DefaultTcpBatch(TId id)
        {
            Id = id;
            _internalList = new List<TResponse>();
        }

        public void Update(TResponse response) => _internalList.Add(response);
        
        public IEnumerator<TResponse> GetEnumerator() => _internalList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}