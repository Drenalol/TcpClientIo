using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Base;

namespace Drenalol.Client
{
    public sealed partial class TcpClientIo<TRequest, TResponse>
    {
        private readonly BlockingCollection<byte[]> _requests;
        private readonly ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>> _responses;
        private readonly ActionBlock<(object, TResponse)> _responseBlock;
        private readonly object _lock = new object();

        /// <summary>
        /// WARNING! This property lock internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Waiters
        {
            get
            {
                lock (_lock)
                {
                    return _responses.Count;
                }
            }
        }

        public int SendQueue => _requests.Count;

        private void AddOrRemoveResponse((object, TResponse) arg)
        {
            var (responseId, response) = arg;
            TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
            TcpPackageBatch<TResponse> packageBatch = null;

            // From MSDN: ConcurrentDictionary<TKey,TValue> is designed for multithreaded scenarios.
            // You do not have to use locks in your code to add or remove items from the collection.
            // However, it is always possible for one thread to retrieve a value, and another thread
            // to immediately update the collection by giving the same key a new value.
            lock (_lock)
            {
                if (_responses.TryRemove(responseId, out tcs))
                {
                    switch (tcs.Task.Status)
                    {
                        case TaskStatus.WaitingForActivation:
                            New(tcs);
                            break;
                        case TaskStatus.RanToCompletion:
                            Exists();
                            break;
                    }
                }
                else
                    New();
            }

            void New(TaskCompletionSource<TcpPackageBatch<TResponse>> innerTcs = null)
            {
                packageBatch = new TcpPackageBatch<TResponse>(responseId, response);
                tcs = innerTcs ?? InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- PackageRead (tcs {(innerTcs == null ? "new" : "exists")}, TaskId: {tcs.Task.Id.ToString()}) " +
                                $"PackageId: {packageBatch.PackageId}, PackageResult.QueueCount: {packageBatch.QueueCount.ToString()}");
            }

            void Exists()
            {
                packageBatch = tcs.Task.Result;
                packageBatch.Enqueue(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- PackageRead (tcs exists, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageBatch.PackageId}, " +
                                $"PackageResult.QueueCount: {packageBatch.QueueCount.ToString()}");
            }

            tcs.SetResult(packageBatch);
        }

        public bool TrySend(TRequest request, int timeout, CancellationToken? token = default) => InternalTrySend(request, timeout, token ?? _baseCancellationToken);
        public bool TrySend(TRequest request) => InternalTrySend(request, 0, new CancellationToken());
        public void Send(TRequest request, CancellationToken? token = default) => InternalTrySend(request, -1, token);

        private bool InternalTrySend(TRequest request, int timeout, CancellationToken? token = default)
        {
            try
            {
                var serializedRequest = _serializer.Serialize(request);
                return !_requests.IsAddingCompleted && _requests.TryAdd(serializedRequest, timeout, token ?? _baseCancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Got {e.GetType()}: {e.Message}");
                throw;
            }
        }

        private TaskCompletionSource<TcpPackageBatch<TResponse>> InternalGetOrAddLazyTcs(object key)
        {
            lock (_lock)
            {
                return _responses.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);                
            }
        }
        
        private static Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>>(() => new TaskCompletionSource<TcpPackageBatch<TResponse>>());
        
        public async Task<TcpPackageBatch<TResponse>> ReceiveAsync(object key, CancellationToken? token = default) => await InternalReceiveAsync(key, token ?? _baseCancellationToken);

        private Task<TcpPackageBatch<TResponse>> InternalReceiveAsync(object key, CancellationToken token)
        {
            TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
            // Info about lock read below in TcpReadAsync method
            lock (_lock)
            {
                if (!_responses.TryRemove(key, out tcs))
                    tcs = InternalGetOrAddLazyTcs(key);
            }

            using (token.Register(() =>
            {
                var cancelled = tcs.TrySetCanceled();
                Debug.WriteLine(cancelled ? $"Cancelled {key}" : $"Not cancelled {key}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return tcs.Task;
            }
        }
    }
}