using System;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.TcpClientIo.Batches;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Serialization;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Client
{
    public partial class TcpClientIo<TId, TInput, TOutput>
    {
        private async Task TcpWriteAsync()
        {
            Exception? internalException = null;
            
            try
            {
                var networkStreamPipeWriterExecutor = CreatePipeWriterExecutor(_options.PipeExecutorOptions, _networkStreamPipeWriter, "NetworkStream");
                
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    
                    var request = await _bufferBlockRequests.ReceiveAsync(_baseCancellationToken);

                    FlushResult writeResult;

                    try
                    {
                        writeResult = await networkStreamPipeWriterExecutor.WriteAsync(request.Raw, _baseCancellationToken);
                    }
                    finally
                    {
                        Interlocked.Add(ref _bytesWrite, request.Raw.Length);
                        request.ReturnRentedArray(TcpSerializerBase.ArrayPool);
                    }

                    if (writeResult.IsCanceled || writeResult.IsCompleted)
                        break;
                }
            }
            catch (OperationCanceledException canceledException)
            {
                internalException = canceledException;
            }
            catch (Exception exception)
            {
                var exceptionType = exception.GetType();
                _logger?.LogCritical(exception, "TcpWriteAsync Got {ExceptionType}, {Message}", exceptionType, exception.Message);
                internalException = exception;
                throw;
            }
            finally
            {
                _pipelineWriteEnded = true;
                StopWriter(internalException);
                _writeResetEvent.Set();
            }
        }

        private async Task TcpReadAsync()
        {
            Exception? internalException = null;
            
            try
            {
                var networkStreamPipeReaderExecutor = CreatePipeReaderExecutor(_options.PipeExecutorOptions, _networkStreamPipeReader, "NetworkStream");
                var deserializePipeWriterExecutor = CreatePipeWriterExecutor(_options.PipeExecutorOptions, _deserializePipeWriter, "Serializer");
                
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    var readResult = await networkStreamPipeReaderExecutor.ReadAsync(_baseCancellationToken);

                    if (readResult.IsCanceled || readResult.IsCompleted)
                        break;
                    
                    if (readResult.Buffer.IsEmpty)
                        continue;

                    foreach (var buffer in readResult.Buffer)
                    {
                        await deserializePipeWriterExecutor.WriteAsync(buffer, _baseCancellationToken);
                        Interlocked.Add(ref _bytesRead, buffer.Length);
                    }
                    
                    networkStreamPipeReaderExecutor.Consume(readResult.Buffer.End);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                internalException = canceledException;
            }
            catch (Exception exception)
            {
                _logger?.LogCritical(exception, "TcpReadAsync catch: {Message}", exception.Message);
                internalException = exception;
                throw;
            }
            finally
            {
                _pipelineReadEnded = true;
                StopReader(internalException);
                _readResetEvent.Set();
            }
        }

        private async Task DeserializeResponseAsync()
        {
            Exception? internalException = null;
            
            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    var (responseId, response) = await _deserializer.DeserializePipeAsync(_baseCancellationToken);
                    await _completeResponses.SetAsync(responseId, _batchRules.Create(response), true);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                internalException = canceledException;
            }
            catch (Exception exception)
            {
                var exceptionType = exception.GetType();
                _logger?.LogCritical(exception, "DeserializeResponseAsync Got {ExceptionType}, {Message}", exceptionType, exception.Message);
                internalException = exception;
                throw;
            }
            finally
            {
                StopDeserializeWriterReader(internalException);
            }
        }

        private void StopDeserializeWriterReader(Exception? exception)
        {
            Diag("Completion Deserializer PipeWriter and PipeReader started");
            _deserializePipeWriter.CancelPendingFlush();
            _deserializePipeReader.CancelPendingRead();

            if (_tcpClient.Client?.Connected == true)
            {
                _deserializePipeWriter.Complete(exception);
                _deserializePipeReader.Complete(exception);
            }
            
            if (!_baseCancellationTokenSource.IsCancellationRequested)
            {
                Diag("Cancelling _baseCancellationTokenSource from StopDeserializeWriterReader");
                _baseCancellationTokenSource.Cancel();
            }

            Diag("Completion Deserializer PipeWriter and PipeReader ended");
        }

        private void StopReader(Exception? exception)
        {
            Diag("Completion NetworkStream PipeReader started");

            foreach (var completedResponse in _completeResponses.Where(tcs => tcs.Value.DelayedTask.Task.Status == TaskStatus.WaitingForActivation))
            {
                var innerException = exception ?? TcpClientIoException.ConnectionBroken;
                Diag($"Set force {innerException.GetType()} in {nameof(TaskCompletionSource<ITcpBatch<TOutput>>)} in {nameof(TaskStatus.WaitingForActivation)}");
                completedResponse.Value.DelayedTask.TrySetException(innerException);
            }

            _networkStreamPipeReader.CancelPendingRead();

            if (_tcpClient.Client?.Connected == true)
                _networkStreamPipeReader.Complete(exception);
            
            if (!_baseCancellationTokenSource.IsCancellationRequested)
            {
                Diag("Cancelling _baseCancellationTokenSource from StopReader");
                _baseCancellationTokenSource.Cancel();
            }

            Diag("Completion NetworkStream PipeReader ended");
        }

        private void StopWriter(Exception? exception)
        {
            Diag("Completion NetworkStream PipeWriter started");

            try
            {
                _networkStreamPipeWriter.CancelPendingFlush();
                
                if (_tcpClient.Client.Connected)
                    _networkStreamPipeWriter.Complete(exception);
            }
            finally
            {
                if (_bufferBlockRequests.TryReceiveAll(out var requests))
                {
                    _bufferBlockRequests.Complete();

                    foreach (var request in requests) 
                        request.ReturnRentedArray(TcpSerializerBase.ArrayPool);
                }
                else
                    _bufferBlockRequests.Complete();
            }

            if (!_baseCancellationTokenSource.IsCancellationRequested)
            {
                Diag("Cancelling _baseCancellationTokenSource from StopWriter");
                _baseCancellationTokenSource.Cancel();
            }

            Diag("Completion NetworkStream PipeWriter ended");
        }
    }
}