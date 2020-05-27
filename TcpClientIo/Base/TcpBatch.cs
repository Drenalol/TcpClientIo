using System.Collections;
using System.Collections.Generic;

namespace Drenalol.Base
{
    public sealed class TcpBatch<T> : ITcpBatch<T>
    {
        private readonly IList<T> _internalList;
        public object Id { get; }
        public int Count => _internalList.Count;

        public TcpBatch(object id)
        {
            Id = id;
            _internalList = new List<T>();
        }

        internal void Add(T input) => _internalList.Add(input);
        
        public IEnumerator<T> GetEnumerator() => _internalList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}