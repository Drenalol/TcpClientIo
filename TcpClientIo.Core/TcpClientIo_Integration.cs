using System;
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo
{
    public partial class TcpClientIo<TId, TRequest, TResponse>
    {
        private async Task SetResponseAsync(TId responseId, TResponse response)
        {
            TaskCompletionSource<ITcpBatch<TId, TResponse>> tcs;
            ITcpBatch<TId, TResponse> batch = null;

            // From MSDN: ConcurrentDictionary<TId,TValue> is designed for multi-threaded scenarios.
            // You do not have to use locks in your code to add or remove items from the collection.
            // However, it is always possible for one thread to retrieve a value, and another thread
            // to immediately update the collection by giving the same id a new value.
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
                _consumingResetEvent.Set();
            }

            void AddOrUpdate(TaskCompletionSource<ITcpBatch<TId, TResponse>> innerTcs = null)
            {
                batch = _batchRules.Create(responseId, response);
                tcs = innerTcs ?? InternalGetOrAddLazyTcs(responseId);
                _logger?.LogInformation($"Available new response: Id {responseId}, create new batch (Count: 1)");
            }

            async Task UpdateAsync()
            {
                batch = await tcs.Task;
                batch.Update(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                _logger?.LogInformation($"Available new response: Id {responseId}, update exists batch (Count: {batch.Count.ToString()})");
            }
        }

        private TaskCompletionSource<ITcpBatch<TId, TResponse>> InternalGetOrAddLazyTcs(TId id)
        {
            return _completeResponses.GetOrAdd(id, _ => InternalCreateLazyTcs().Value);

            Lazy<TaskCompletionSource<ITcpBatch<TId, TResponse>>> InternalCreateLazyTcs() =>
                new Lazy<TaskCompletionSource<ITcpBatch<TId, TResponse>>>(() => new TaskCompletionSource<ITcpBatch<TId, TResponse>>());
        }

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

        /// <summary>
        /// Begins an asynchronous request to receive response associated with the specified <see cref="TId"/> from a connected <see cref="TcpClientIo{TId,TRequest,TResponse}"/> object.
        /// <para> </para>
        /// WARNING! Identifier is strongly-typed, if Id of <see cref="TRequest"/> have type uint, you must pass it value in uint too, otherwise the call will be forever or cancelled by <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="responseId"></param>
        /// <param name="token"></param>
        /// <returns><see cref="ITcpBatch{TId, TResponse}"/></returns>
        public async Task<ITcpBatch<TId, TResponse>> ReceiveAsync(TId responseId, CancellationToken token = default)
        {
            var internalToken = token == default ? _baseCancellationToken : token;
            TaskCompletionSource<ITcpBatch<TId, TResponse>> tcs;
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
                var result = await tcs.Task;
                return result;
            }
        }

#if NETSTANDARD2_1
        /// <summary>Provides a consuming <see cref="T:System.Collections.Generics.IAsyncEnumerable{T}"/> for <see cref="ITcpBatch{TId, TResponse}"/> in the collection.
        /// Calling MoveNextAsync on the returned enumerable will block if there is no data available, or will
        /// throw an <see cref="System.OperationCanceledException"/> if the <see cref="CancellationToken"/> is canceled.
        /// </summary>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the <see cref="token"/> is canceled.</exception>
        public async IAsyncEnumerable<ITcpBatch<TId, TResponse>> GetConsumingAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
        {
            CancellationTokenSource internalCts = null;
            CancellationToken internalToken;

            if (token != default)
            {
                internalCts = CancellationTokenSource.CreateLinkedTokenSource(_baseCancellationToken, token);
                internalToken = internalCts.Token;
            }
            else
                internalToken = _baseCancellationToken;

            while (!internalToken.IsCancellationRequested)
            {
                IList<ITcpBatch<TId, TResponse>> result;

                try
                {
                    internalToken.ThrowIfCancellationRequested();
                    KeyValuePair<TId, TaskCompletionSource<ITcpBatch<TId, TResponse>>>[] completedResponses;

                    // Info about lock read in SetResponseAsync method
                    using (await _asyncLock.LockAsync(internalToken))
                    {
                        completedResponses = _completeResponses
                            .ToArray() // This trick is cheaper than calling ConcurrentDictionary.Where.
                            .Where(p => p.Value.Task.Status == TaskStatus.RanToCompletion)
                            .ToArray();
                    }

                    if (completedResponses.Length == 0)
                    {
                        await _consumingResetEvent.WaitAsync(internalToken);
                        _consumingResetEvent.Reset();
                        continue;
                    }

                    using (await _asyncLock.LockAsync(internalToken))
                    {
                        result = new List<ITcpBatch<TId, TResponse>>();

                        foreach (var pair in completedResponses)
                        {
                            internalToken.ThrowIfCancellationRequested();

                            if (_completeResponses.TryRemove(pair.Key, out var tcs))
                            {
                                result.Add(await tcs.Task);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger?.LogCritical($"{nameof(GetConsumingAsyncEnumerable)} Got {exception.GetType()}, {exception}");
                    throw;
                }

                foreach (var batch in result)
                {
                    yield return batch;
                }
            }

            internalCts?.Dispose();
        }

        /// <summary>Provides a consuming <see cref="T:System.Collections.Generics.IAsyncEnumerable{T}"/> for <see cref="TResponse"/> in the collection.
        /// Calling MoveNextAsync on the returned enumerable will block if there is no data available, or will
        /// throw an <see cref="System.OperationCanceledException"/> if the <see cref="CancellationToken"/> is canceled.
        /// </summary>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the <see cref="token"/> is canceled.</exception>
        public async IAsyncEnumerable<TResponse> GetExpandableConsumingAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var batch in GetConsumingAsyncEnumerable(token))
            {
                foreach (var response in batch)
                {
                    yield return response;
                }
            }
        }
#endif
    }
}