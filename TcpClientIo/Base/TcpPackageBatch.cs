using System.Collections.Concurrent;

namespace Drenalol.Base
{
    public sealed class TcpPackageBatch<T>
    {
        private readonly ConcurrentQueue<T> _internalQueue;
        public object PackageId { get; }
        public int QueueCount => _internalQueue.Count;
        public bool IsSingle => QueueCount == 1;

        public TcpPackageBatch(object packageId, T initialItem)
        {
            PackageId = packageId;
            _internalQueue = new ConcurrentQueue<T>();
            Enqueue(initialItem);
        }

        internal void Enqueue(T package) => _internalQueue.Enqueue(package);
        
        public bool TryDequeue(out T package) => _internalQueue.TryDequeue(out package);
    }
}