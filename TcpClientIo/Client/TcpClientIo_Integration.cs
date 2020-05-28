using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Base;
using Microsoft.Extensions.Logging;

namespace Drenalol.Client
{
    public partial class TcpClientIo<TRequest, TResponse>
    {
        private async Task SetResponseAsync(object responseId, TResponse response)
        {
            TaskCompletionSource<ITcpBatch<TResponse>> tcs;
            ITcpBatch<TResponse> batch = null;

            // From MSDN: ConcurrentDictionary<TKey,TValue> is designed for multi-threaded scenarios.
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
                _logger?.LogInformation($"Available new response: Id {responseId}, create new batch (Count: 1)");
            }

            async Task UpdateAsync()
            {
                batch = await tcs.Task;
                ((TcpBatch<TResponse>) batch).Add(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                _logger?.LogInformation($"Available new response: Id {responseId}, update exists batch (Count: {batch.Count.ToString()})");
            }
        }

        private TaskCompletionSource<ITcpBatch<TResponse>> InternalGetOrAddLazyTcs(object key)
        {
            return _completeResponses.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);
            Lazy<TaskCompletionSource<ITcpBatch<TResponse>>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<ITcpBatch<TResponse>>>(() => new TaskCompletionSource<ITcpBatch<TResponse>>());
        }

        public override Task SendAsync(object request, CancellationToken token = default) => SendAsync((TRequest) request, token);

        /// <summary>
        /// Serialize and sends data asynchronously to a connected <see cref="TcpClientIo{TRequest,TResponse}"/> object.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> SendAsync(TRequest request, CancellationToken token = default)
        {
            try
            {
                var serializedRequest = _serializer.Serialize(request);
                return await _bufferBlockRequests.SendAsync(serializedRequest, token == default ? _baseCancellationToken : token);
            }
            catch (Exception e)
            {
                _logger?.LogError($"{nameof(SendAsync)} Got {e.GetType()}: {e.Message}");
                throw;
            }
        }

        // ReSharper disable once MethodOverloadWithOptionalParameter
        public override async Task<object> ReceiveAsync(object responseId, CancellationToken token = default, bool isObject = true) => await ReceiveAsync(responseId, token);

        /// <summary>
        /// Begins an asynchronous request to receive response associated with the specified responseId from a connected <see cref="TcpClientIo{TRequest,TResponse}"/> object.
        /// <para> </para>
        /// WARNING! Identifier is strongly-typed, if Id of <see cref="TRequest"/> have type uint, you must pass it value in uint too, otherwise the call will be forever or cancelled by <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="responseId"></param>
        /// <param name="token"></param>
        /// <returns><see cref="ITcpBatch{T}"/></returns>
        public async Task<ITcpBatch<TResponse>> ReceiveAsync(object responseId, CancellationToken token = default)
        {
            var internalToken = token == default ? _baseCancellationToken : token;
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
                _logger?.LogInformation(cancelled ? $"Response has been cancelled successfulfy: Id {responseId}" : $"Response cancellation was failed: Id {responseId}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return await tcs.Task;
            }
        }
    }
}