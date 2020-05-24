using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Base;

namespace Drenalol.Client
{
    public partial class TcpClientIo<TRequest, TResponse>
    {
        private async Task SetResponseAsync(object responseId, TResponse response)
        {
            TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
            TcpPackageBatch<TResponse> packageBatch = null;

            // From MSDN: ConcurrentDictionary<TKey,TValue> is designed for multithreaded scenarios.
            // You do not have to use locks in your code to add or remove items from the collection.
            // However, it is always possible for one thread to retrieve a value, and another thread
            // to immediately update the collection by giving the same key a new value.
            using (await _asyncLock.LockAsync())
            {
                if (_completeResponses.TryRemove(responseId, out tcs))
                {
                    switch (tcs.Task.Status)
                    {
                        case TaskStatus.WaitingForActivation:
                            AddOrUpdate(tcs);
                            break;
                        case TaskStatus.RanToCompletion:
                            await UpdateAsync();
                            break;
                    }
                }
                else
                    AddOrUpdate();
                
                tcs.SetResult(packageBatch);
            }

            void AddOrUpdate(TaskCompletionSource<TcpPackageBatch<TResponse>> innerTcs = null)
            {
                packageBatch = new TcpPackageBatch<TResponse>(responseId, response);
                tcs = innerTcs ?? InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- {nameof(SetResponseAsync)} (tcs {(innerTcs == null ? "new" : "exists")}, TaskId: {tcs.Task.Id.ToString()}) " +
                                $"PackageId: {packageBatch.PackageId}, PackageResult.QueueCount: {packageBatch.Count.ToString()}");
            }

            async Task UpdateAsync()
            {
                packageBatch = await tcs.Task;
                packageBatch.Add(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- {nameof(SetResponseAsync)} (tcs exists, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageBatch.PackageId}, " +
                                $"PackageResult.QueueCount: {packageBatch.Count.ToString()}");
            }
        }

        private TaskCompletionSource<TcpPackageBatch<TResponse>> InternalGetOrAddLazyTcs(object key)
        {
            return _completeResponses.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);
            static Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>>(() => new TaskCompletionSource<TcpPackageBatch<TResponse>>());
        }
        
        public async Task SendAsync(TRequest request, CancellationToken? token = default)
        {
            try
            {
                var serializedRequest = _serializer.Serialize(request);
                var sended = await _bufferBlockRequests.SendAsync(serializedRequest, token ?? _baseCancellationToken);
                Debug.WriteLineIf(!sended, $"{nameof(SendAsync)} failed");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{nameof(SendAsync)} Got {e.GetType()}: {e.Message}");
                throw;
            }
        }
        
        public async Task<TcpPackageBatch<TResponse>> ReceiveAsync(object key, CancellationToken? token = default)
        {
            var internalToken = token ?? _baseCancellationToken;
            TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
            // Info about lock read in SetResponseAsync method
            using (await _asyncLock.LockAsync())
            {
                if (!_completeResponses.TryRemove(key, out tcs))
                {
                    tcs = InternalGetOrAddLazyTcs(key);
                }
            }

            await using (internalToken.Register(() =>
            {
                var cancelled = tcs.TrySetCanceled();
                Debug.WriteLine(cancelled ? $"Cancelled {key}" : $"Not cancelled {key}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return await tcs.Task;
            }
        }
    }
}