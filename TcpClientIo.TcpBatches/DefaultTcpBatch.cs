using System.Collections;
using System.Collections.Generic;

namespace TcpClientIo.TcpBatchRules
{
    /// <summary>
    /// Default TcpBatch instance
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class DefaultTcpBatch<T> : ITcpBatch<T>
    {
        private readonly IList<T> _internalList;
        public object Id { get; }
        public int Count => _internalList.Count;

        public DefaultTcpBatch(object id)
        {
            Id = id;
            _internalList = new List<T>();
        }

        public void Update(T response) => _internalList.Add(response);
        
        public IEnumerator<T> GetEnumerator() => _internalList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}