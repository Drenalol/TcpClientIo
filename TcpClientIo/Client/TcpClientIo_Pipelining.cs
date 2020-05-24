using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Drenalol.Client
{
    public partial class TcpClientIo<TRequest, TResponse>
    {
        private async Task TcpWriteAsync()
        {
            await _semaphore.WaitAsync(_baseCancellationToken);

            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();

                    if (!await _bufferBlockRequests.OutputAvailableAsync(_baseCancellationToken))
                        continue;

                    var bytesArray = await _bufferBlockRequests.ReceiveAsync(_baseCancellationToken);
                    await _networkStreamPipeWriter.WriteAsync(bytesArray, _baseCancellationToken);
                    BytesWrite += (ulong) bytesArray.Length;
                    Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} -> {nameof(TcpWriteAsync)} {bytesArray.Length.ToString()} bytes");
                }
            }
            catch (OperationCanceledException canceledException)
            {
                _internalException = canceledException;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"{nameof(TcpWriteAsync)} Got {exception.GetType()}, {exception.Message}");
                _internalException = exception;
                throw;
            }
            finally
            {
                StopWriter(_internalException);
                _semaphore.Release();
            }
        }

        private async Task TcpReadAsync()
        {
            await _semaphore.WaitAsync(_baseCancellationToken);

            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    var readResult = await _networkStreamPipeReader.ReadAsync(_baseCancellationToken);
                    
                    if (readResult.Buffer.IsEmpty)
                        continue;

                    if (readResult.Buffer.IsSingleSegment)
                    {
                        var buffer = readResult.Buffer.First;
                        await _deserializePipeWriter.WriteAsync(buffer.ToArray(), _baseCancellationToken);
                        BytesRead += (ulong) buffer.Length;
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- {nameof(TcpReadAsync)} {buffer.Length.ToString()} bytes");
                    }
                    else
                    {
                        foreach (var buffer in readResult.Buffer)
                        {
                            await _deserializePipeWriter.WriteAsync(buffer.ToArray(), _baseCancellationToken);
                            BytesRead += (ulong) buffer.Length;
                            Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- {nameof(TcpReadAsync)} {buffer.Length.ToString()} bytes");
                        }
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
                Debug.WriteLine($"{nameof(TcpReadAsync)} Got {exception.GetType()}, {exception}");
                _internalException = exception;
                throw;
            }
            finally
            {
                StopReader(_internalException);
                _semaphore.Release();
            }
        }

        private async Task DeserializeResponseAsync()
        {
            try
            {
                while (true)
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
                    var (responseId, responseSize, response) = await _serializer.DeserializeAsync(_deserializePipeReader, _baseCancellationToken);
                    Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {_id.ToString()} {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- {nameof(DeserializeResponseAsync)} {responseSize.ToString()} bytes");
                    await SetResponseAsync(responseId, response);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                _internalException = canceledException;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"{nameof(DeserializeResponseAsync)} Got {exception.GetType()}, {exception}");
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
            Debug.WriteLine("Stopping deserialize reader/writer");
            _deserializePipeWriter.CancelPendingFlush();
            _deserializePipeReader.CancelPendingRead();

            if (_tcpClient.Client.Connected)
            {
                _deserializePipeWriter.Complete(exception);
                _deserializePipeReader.Complete(exception);
            }

            Debug.WriteLine("Stopping deserialize reader/writer end");
        }

        private void StopReader(Exception exception)
        {
            Debug.WriteLine("Stopping reader");
            foreach (var (_, tcs) in _completeResponses.Where(tcs => tcs.Value.Task.Status == TaskStatus.WaitingForActivation))
            {
                var innerException = exception ?? new OperationCanceledException();
                Debug.WriteLine($"Set force {innerException.GetType()} in TaskCompletionSource in TaskStatus.WaitingForActivation");
                tcs.TrySetException(innerException);
            }

            _networkStreamPipeReader.CancelPendingRead();

            if (_tcpClient.Client.Connected)
                _networkStreamPipeReader.Complete(exception);

            Debug.WriteLine("Stopping reader end");
        }

        private void StopWriter(Exception exception)
        {
            Debug.WriteLine("Stopping writer");
            _networkStreamPipeWriter.CancelPendingFlush();

            if (_tcpClient.Client.Connected)
                _networkStreamPipeWriter.Complete(exception);

            Debug.WriteLine("Stopping writer end");
        }
    }
}