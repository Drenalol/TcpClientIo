using System;
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.TcpClientIo.Batches;
using Drenalol.TcpClientIo.Exceptions;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Client
{
    public partial class TcpClientIo<TId, TRequest, TResponse>
    {
        private async Task SetResponseAsync(TId responseId, TResponse response)
        {
            TaskCompletionSource<ITcpBatch<TResponse>> tcs;
            ITcpBatch<TResponse> batch = null;

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

                tcs.TrySetResult(batch);
                _consumingResetEvent.Set();
            }

            void AddOrUpdate(TaskCompletionSource<ITcpBatch<TResponse>> innerTcs = null)
            {
                batch = _batchRules.Create(response);
                tcs = innerTcs ?? InternalGetOrAddLazyTcs(responseId);
                _logger?.LogInformation($"Available new response: Id {responseId}, create new batch (Count: 1)");
            }

            async Task UpdateAsync()
            {
                batch = await tcs.Task;
                batch.Add(response);
                tcs = InternalGetOrAddLazyTcs(responseId);
                _logger?.LogInformation($"Available new response: Id {responseId}, update exists batch (Count: {batch.Count.ToString()})");
            }
        }

        private TaskCompletionSource<ITcpBatch<TResponse>> InternalGetOrAddLazyTcs(TId id)
        {
            return _completeResponses.GetOrAdd(id, _ => InternalCreateLazyTcs().Value);

            Lazy<TaskCompletionSource<ITcpBatch<TResponse>>> InternalCreateLazyTcs() =>
                new Lazy<TaskCompletionSource<ITcpBatch<TResponse>>>(() => new TaskCompletionSource<ITcpBatch<TResponse>>());
        }

        /// <summary>
        /// Serialize and sends data asynchronously to a connected <see cref="TcpClientIo{TRequest,TResponse}"/> object.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="token"></param>
        /// <returns><see cref="bool"/></returns>
        /// <exception cref="TcpClientIoException"></exception>
        public async Task<bool> SendAsync(TRequest request, CancellationToken token = default)
        {
            try
            {
                if (_disposing)
                    throw new ObjectDisposedException(nameof(_tcpClient));
                
                if (!_disposing && _pipelineWriteEnded)
                    throw TcpClientIoException.ConnectionBroken();

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
        /// Begins an asynchronous request to receive response associated with the specified ID from a connected <see cref="TcpClientIo{TId,TRequest,TResponse}"/> object.
        /// </summary>
        /// <param name="responseId"></param>
        /// <param name="token"></param>
        /// <returns><see cref="ITcpBatch{TResponse}"/></returns>
        /// <exception cref="TcpClientIoException"></exception>
        public async Task<ITcpBatch<TResponse>> ReceiveAsync(TId responseId, CancellationToken token = default)
        {
            if (_disposing)
                throw new ObjectDisposedException(nameof(_tcpClient));
            
            if (!_disposing && _pipelineReadEnded)
                throw TcpClientIoException.ConnectionBroken();

            var hasOwnToken = false;
            CancellationToken internalToken;

            if (token == default)
                internalToken = _baseCancellationToken;
            else
            {
                internalToken = token;
                hasOwnToken = true;
            }

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
                if (_disposing || hasOwnToken)
                    tcs.TrySetCanceled();
                else if (!_disposing && _pipelineReadEnded)
                    tcs.TrySetException(TcpClientIoException.ConnectionBroken());
            }))
            {
                var result = await tcs.Task;
                return result;
            }
        }

#if NETSTANDARD2_1
        /// <summary>Provides a consuming <see cref="T:System.Collections.Generics.IAsyncEnumerable{T}"/> for <see cref="ITcpBatch{TResponse}"/> in the collection.
        /// Calling MoveNextAsync on the returned enumerable will block if there is no data available, or will
        /// throw an <see cref="System.OperationCanceledException"/> if the <see cref="CancellationToken"/> is canceled.
        /// </summary>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken"/> is canceled.</exception>
        /// <exception cref="TcpClientIoException"></exception>
        public async IAsyncEnumerable<ITcpBatch<TResponse>> GetConsumingAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
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
                IList<ITcpBatch<TResponse>> result;

                try
                {
                    internalToken.ThrowIfCancellationRequested();
                    KeyValuePair<TId, TaskCompletionSource<ITcpBatch<TResponse>>>[] completedResponses;

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
                        result = new List<ITcpBatch<TResponse>>();

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
                    if (!_disposing && _pipelineReadEnded)
                        throw TcpClientIoException.ConnectionBroken();
                    
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

        /// <summary>Provides a consuming <see cref="T:System.Collections.Generics.IAsyncEnumerable{T}"/> for Response in the collection.
        /// Calling MoveNextAsync on the returned enumerable will block if there is no data available, or will
        /// throw an <see cref="System.OperationCanceledException"/> if the <see cref="CancellationToken"/> is canceled.
        /// </summary>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken"/> is canceled.</exception>
        /// <exception cref="TcpClientIoException"></exception>
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