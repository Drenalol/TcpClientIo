using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.TcpClientIo.Batches;
using Drenalol.TcpClientIo.Exceptions;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Client
{
    public partial class TcpClientIo<TId, TInput, TOutput>
    {
        /// <summary>
        /// Serialize and sends data asynchronously to a connected <see cref="TcpClientIo{TRequest,TResponse}"/> object.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="token"></param>
        /// <returns><see cref="bool"/></returns>
        /// <exception cref="TcpClientIoException"></exception>
        public async Task<bool> SendAsync(TInput input, CancellationToken token = default)
        {
            try
            {
                if (_disposing)
                    throw new ObjectDisposedException(nameof(_tcpClient));

                if (!_disposing && _pipelineWriteEnded)
                    throw TcpClientIoException.ConnectionBroken;

                var serializedRequest = _serializer.Serialize(input);
                return await _bufferBlockRequests.SendAsync(serializedRequest, token == default ? _baseCancellationToken : token);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "SendAsync catch: {Message}", e.Message);
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
        public async Task<ITcpBatch<TOutput>> ReceiveAsync(TId responseId, CancellationToken token = default)
        {
            if (_disposing)
                throw new ObjectDisposedException(nameof(_tcpClient));

            if (!_disposing && _pipelineReadEnded)
                throw TcpClientIoException.ConnectionBroken;

            return await _completeResponses.WaitAsync(responseId, token);
        }

        /// <summary>Provides a consuming <see cref="T:System.Collections.Generics.IAsyncEnumerable{T}"/> for <see cref="ITcpBatch{TResponse}"/> in the collection.
        /// Calling MoveNextAsync on the returned enumerable will block if there is no data available, or will
        /// throw an <see cref="System.OperationCanceledException"/> if the <see cref="CancellationToken"/> is canceled.
        /// </summary>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken"/> is canceled.</exception>
        /// <exception cref="TcpClientIoException"></exception>
        public async IAsyncEnumerable<ITcpBatch<TOutput>> GetConsumingAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
        {
            CancellationTokenSource? internalCts = null;
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
                IList<ITcpBatch<TOutput>> result;

                try
                {
                    internalToken.ThrowIfCancellationRequested();

                    var completedResponses = _completeResponses
                        .Filter(p => p.Value.DelayedTask.Task.Status == TaskStatus.RanToCompletion)
                        .ToArray();

                    if (completedResponses.Length == 0)
                    {
                        await _consumingResetEvent.WaitAsync(internalToken);
                        _consumingResetEvent.Reset();
                        continue;
                    }

                    result = new List<ITcpBatch<TOutput>>();

                    foreach (var (key, tcs) in completedResponses)
                    {
                        internalToken.ThrowIfCancellationRequested();

                        if (await _completeResponses.TryRemoveAsync(key)) 
                            result.Add(await tcs.DelayedTask.Task);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!_disposing && _pipelineReadEnded)
                        throw TcpClientIoException.ConnectionBroken;

                    throw;
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "GetConsumingAsyncEnumerable catch: {Message}", exception.Message);
                    throw;
                }

                foreach (var batch in result)
                    yield return batch;
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
        public async IAsyncEnumerable<TOutput> GetExpandableConsumingAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var batch in GetConsumingAsyncEnumerable(token))
            {
                foreach (var response in batch)
                {
                    yield return response;
                }
            }
        }
    }
}