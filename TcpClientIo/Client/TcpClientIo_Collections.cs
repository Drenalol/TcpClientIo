using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Base;
using Nito.AsyncEx;

namespace Drenalol.Client
{
    public sealed partial class TcpClientIo<TRequest, TResponse>
    {
        private readonly BufferBlock<byte[]> _requests;
        private readonly ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>> _completeResponses;
        private readonly ActionBlock<(object, TResponse)> _responses;
        private readonly AsyncLock _asyncLock = new AsyncLock();

        /// <summary>
        /// WARNING! This property lock internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Waiters => _completeResponses.Count;

        public int SendQueue => _requests.Count;

        private async Task AddOrRemoveResponseAsync((object, TResponse) arg)
        {
            var (responseId, response) = arg;
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
                            New(tcs);
                            break;
                        case TaskStatus.RanToCompletion:
                            await ExistsAsync();
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

            async Task ExistsAsync()
            {
                packageBatch = await tcs.Task;
                packageBatch.Enqueue(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- PackageRead (tcs exists, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageBatch.PackageId}, " +
                                $"PackageResult.QueueCount: {packageBatch.QueueCount.ToString()}");
            }

            tcs.SetResult(packageBatch);
        }

        public async Task SendAsync(TRequest request, CancellationToken? token = default)
        {
            try
            {
                var serializedRequest = _serializer.Serialize(request);
                await _requests.SendAsync(serializedRequest, token ?? _baseCancellationToken);
            }
            catch (ObjectDisposedException)
            {
                //
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Got {e.GetType()}: {e.Message}");
                throw;
            }
        }

        private TaskCompletionSource<TcpPackageBatch<TResponse>> InternalGetOrAddLazyTcs(object key) => _completeResponses.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);

        private static Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>>(() => new TaskCompletionSource<TcpPackageBatch<TResponse>>());

        public async Task<TcpPackageBatch<TResponse>> ReceiveAsync(object key, CancellationToken? token = default)
        {
            var internalToken = token ?? _baseCancellationToken;

            TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
            // Info about lock read in TcpReadAsync method
            using (await _asyncLock.LockAsync())
            {
                if (!_completeResponses.TryRemove(key, out tcs))
                    tcs = InternalGetOrAddLazyTcs(key);
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