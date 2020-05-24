using System.Collections;
using System.Collections.Generic;

namespace Drenalol.Base
{
    public sealed class TcpPackageBatch<T> : IEnumerable<T>
    {
        private readonly List<T> _internalList;
        public object PackageId { get; }
        public int Count => _internalList.Count;

        public TcpPackageBatch(object packageId, T initialItem)
        {
            PackageId = packageId;
            _internalList = new List<T>();
            Add(initialItem);
        }

        internal void Add(T package) => _internalList.Add(package);
        
        public IEnumerator<T> GetEnumerator() => _internalList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}