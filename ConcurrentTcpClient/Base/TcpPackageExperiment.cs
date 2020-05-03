using System.Collections.Immutable;
using System.Linq;

namespace Drenalol.Base
{
    // TODO Реализовать структуру с атрибутами для мапинга, и коллекцию с полем IsSingle (тру/фолс) в случае если пришёл пакет с уже имеющиймся ключем
    public sealed class TcpPackageExperiment
    {
        private ImmutableQueue<TcpPackage> _internalQueue;
        public uint PackageId { get; }
        public int QueueCount => _internalQueue.Count();
        public bool IsSingle => QueueCount == 1;

        public TcpPackageExperiment(TcpPackage initialItem)
        {
            PackageId = initialItem.PackageId;
            _internalQueue = ImmutableQueue<TcpPackage>.Empty;
            Enqueue(initialItem);
        }

        public void Enqueue(TcpPackage package) => ImmutableInterlocked.Enqueue(ref _internalQueue, package);
        
        public bool TryDequeue(out TcpPackage package) => ImmutableInterlocked.TryDequeue(ref _internalQueue, out package);
    }
}