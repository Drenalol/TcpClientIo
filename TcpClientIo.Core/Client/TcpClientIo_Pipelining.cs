using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.TcpClientIo.Batches;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Client
{
    public partial class TcpClientIo<TId, TRequest, TResponse>
    {
        private async Task TcpWriteAsync()
        {
            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();

                    if (!await _bufferBlockRequests.OutputAvailableAsync(_baseCancellationToken))
                        continue;

                    var serializedRequest = await _bufferBlockRequests.ReceiveAsync(_baseCancellationToken);
                    var writeResult = await _networkStreamPipeWriter.WriteAsync(serializedRequest.Request, _baseCancellationToken);
                    serializedRequest.ReturnRentedArrays(_arrayPool, true);
                    
                    if (writeResult.IsCanceled || writeResult.IsCompleted)
                        break;
                    
                    Interlocked.Add(ref _bytesWrite, serializedRequest.Request.Length);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                _internalException = canceledException;
            }
            catch (Exception exception)
            {
                var exceptionType = exception.GetType();
                _logger?.LogCritical("TcpWriteAsync Got {ExceptionType}, {Message}", exceptionType, exception.Message);
                _internalException = exception;
                throw;
            }
            finally
            {
                _pipelineWriteEnded = true;
                StopWriter(_internalException);
                _writeResetEvent.Set();
            }
        }

        private async Task TcpReadAsync()
        {
            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    var readResult = await _networkStreamPipeReader.ReadAsync(_baseCancellationToken);

                    if (readResult.IsCanceled || readResult.IsCompleted)
                        break;
                    
                    if (readResult.Buffer.IsEmpty)
                        continue;

                    foreach (var buffer in readResult.Buffer)
                    {
                        await _deserializePipeWriter.WriteAsync(buffer, _baseCancellationToken);
                        Interlocked.Add(ref _bytesRead, buffer.Length);
                    }
                    
                    _networkStreamPipeReader.AdvanceTo(readResult.Buffer.End);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                _internalException = canceledException;
            }
            catch (Exception exception)
            {
                var exceptionType = exception.GetType();
                _logger?.LogCritical("TcpReadAsync Got {ExceptionType}, {Message}", exceptionType, exception.Message);
                _internalException = exception;
                throw;
            }
            finally
            {
                _pipelineReadEnded = true;
                StopReader(_internalException);
                _readResetEvent.Set();
            }
        }

        private async Task DeserializeResponseAsync()
        {
            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    var (responseId, response) = await _deserializer.DeserializeAsync(_deserializePipeReader, _baseCancellationToken);
                    await _completeResponses.SetAsync(responseId, _batchRules.Create(response), true);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                _internalException = canceledException;
            }
            catch (Exception exception)
            {
                var exceptionType = exception.GetType();
                _logger?.LogCritical("DeserializeResponseAsync Got {ExceptionType}, {Message}", exceptionType, exception.Message);
                _internalException = exception;
                throw;
            }
            finally
            {
                StopDeserializeWriterReader(_internalException);
            }
        }

        private void StopDeserializeWriterReader(Exception exception)
        {
            Debug.WriteLine("Completion Deserializer PipeWriter and PipeReader started");
            _deserializePipeWriter.CancelPendingFlush();
            _deserializePipeReader.CancelPendingRead();

            if (_tcpClient.Client.Connected)
            {
                _deserializePipeWriter.Complete(exception);
                _deserializePipeReader.Complete(exception);
            }
            
            if (!_baseCancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine("Cancelling _baseCancellationTokenSource from StopDeserializeWriterReader");
                _baseCancellationTokenSource.Cancel();
            }

            Debug.WriteLine("Completion Deserializer PipeWriter and PipeReader ended");
        }

        private void StopReader(Exception exception)
        {
            Debug.WriteLine("Completion NetworkStream PipeReader started");

            if (_disposing)
            {
                foreach (var completedResponse in _completeResponses.Where(tcs => tcs.Value.Task.Status == TaskStatus.WaitingForActivation))
                {
                    var innerException = exception ?? new OperationCanceledException();
                    Debug.WriteLine($"Set force {innerException.GetType()} in {nameof(TaskCompletionSource<ITcpBatch<TResponse>>)} in {nameof(TaskStatus.WaitingForActivation)}");
                    completedResponse.Value.TrySetException(innerException);
                }
            }

            _networkStreamPipeReader.CancelPendingRead();

            if (_tcpClient.Client.Connected)
                _networkStreamPipeReader.Complete(exception);
            
            if (!_baseCancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine("Cancelling _baseCancellationTokenSource from StopReader");
                _baseCancellationTokenSource.Cancel();
            }

            Debug.WriteLine("Completion NetworkStream PipeReader ended");
        }

        private void StopWriter(Exception exception)
        {
            Debug.WriteLine("Completion NetworkStream PipeWriter started");
            _networkStreamPipeWriter.CancelPendingFlush();

            if (_tcpClient.Client.Connected)
                _networkStreamPipeWriter.Complete(exception);
            
            _bufferBlockRequests.Complete();

            if (!_baseCancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine("Cancelling _baseCancellationTokenSource from StopWriter");
                _baseCancellationTokenSource.Cancel();
            }

            Debug.WriteLine("Completion NetworkStream PipeWriter ended");
        }
    }
}