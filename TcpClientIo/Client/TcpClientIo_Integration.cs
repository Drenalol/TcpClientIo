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
            TaskCompletionSource<ITcpBatch<TResponse>> tcs;
            ITcpBatch<TResponse> batch = null;

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
                
                tcs.SetResult(batch);
            }

            void AddOrUpdate(TaskCompletionSource<ITcpBatch<TResponse>> innerTcs = null)
            {
                batch = new TcpBatch<TResponse>(responseId) {response};
                tcs = innerTcs ?? InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- {nameof(SetResponseAsync)} (tcs {(innerTcs == null ? "new" : "exists")}, TaskId: {tcs.Task.Id.ToString()}) " +
                                $"Id: {batch.Id}, Batch.Count: {batch.Count.ToString()}");
            }

            async Task UpdateAsync()
            {
                batch = await tcs.Task;
                ((TcpBatch<TResponse>) batch).Add(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                $"<- {nameof(SetResponseAsync)} (tcs exists, TaskId: {tcs.Task.Id.ToString()}) Id: {batch.Id}, " +
                                $"Batch.Count: {batch.Count.ToString()}");
            }
        }

        private TaskCompletionSource<ITcpBatch<TResponse>> InternalGetOrAddLazyTcs(object key)
        {
            return _completeResponses.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);
            Lazy<TaskCompletionSource<ITcpBatch<TResponse>>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<ITcpBatch<TResponse>>>(() => new TaskCompletionSource<ITcpBatch<TResponse>>());
        }

        public override Task SendAsync(object request, CancellationToken token = default) => SendAsync((TRequest) request, token);

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

        public override async Task<object> ReceiveAsync(object responseId, CancellationToken token = default, bool isObject = true) => await ReceiveAsync(responseId, token);

        public async Task<ITcpBatch<TResponse>> ReceiveAsync(object responseId, CancellationToken? token = default)
        {
            var internalToken = token ?? _baseCancellationToken;
            TaskCompletionSource<ITcpBatch<TResponse>> tcs;
            // Info about lock read in SetResponseAsync method
            using (await _asyncLock.LockAsync())
            {
                if (!_completeResponses.TryRemove(responseId, out tcs))
                {
                    tcs = InternalGetOrAddLazyTcs(responseId);
                }
            }
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            await using (internalToken.Register(() =>
#else
            using (internalToken.Register(() =>
#endif
            {
                var cancelled = tcs.TrySetCanceled();
                Debug.WriteLine(cancelled ? $"Cancelled {responseId}" : $"Not cancelled {responseId}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return await tcs.Task;
            }
        }
    }
}