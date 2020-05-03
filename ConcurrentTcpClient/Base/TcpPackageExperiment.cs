using System.Collections.Concurrent;

namespace Drenalol.Base
{
    // TODO Реализовать структуру с атрибутами для мапинга, и коллекцию с полем IsSingle (тру/фолс) в случае если пришёл пакет с уже имеющиймся ключем
    public sealed class TcpPackageExperiment
    {
        private readonly ConcurrentQueue<TcpPackage> _internalQueue;
        public uint PackageId { get; }
        public int QueueCount => _internalQueue.Count;
        public bool IsSingle => QueueCount == 1;

        public TcpPackageExperiment(TcpPackage initialItem)
        {
            PackageId = initialItem.PackageId;
            _internalQueue = new ConcurrentQueue<TcpPackage>();
            Enqueue(initialItem);
        }

        public void Enqueue(TcpPackage package) => _internalQueue.Enqueue(package);
        
        public bool TryDequeue(out TcpPackage package) => _internalQueue.TryDequeue(out package);
    }
}