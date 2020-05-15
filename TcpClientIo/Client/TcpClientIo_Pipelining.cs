using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Drenalol.Base;

namespace Drenalol.Client
{
    public sealed partial class TcpClientIo<TRequest, TResponse>
    {
        private readonly TcpPackageSerializer<TRequest, TResponse> _serializer;
        private readonly SemaphoreSlim _semaphore;
        private Exception _internalException;
        private PipeReader Reader { get; set; }
        private PipeWriter Writer { get; set; }
        PipeReader IDuplexPipe.Input => Reader;
        PipeWriter IDuplexPipe.Output => Writer;

        private async Task TcpWriteAsync()
        {
            await _semaphore.WaitAsync(_baseCancellationToken);

            try
            {
                while (await _requests.OutputAvailableAsync(_baseCancellationToken))
                {
                    var bytesArray = await _requests.ReceiveAsync(_baseCancellationToken);
                    await Writer.WriteAsync(bytesArray, _baseCancellationToken);
                    Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} -> PackageWrite {bytesArray.Length.ToString()} bytes");
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
                    var (responseId, response) = await _serializer.DeserializeAsync(Reader, _baseCancellationToken);

                    if (response == null)
                    {
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff}) <- package == null, waiters: {_completeResponses.Count.ToString()}");
                        continue;
                    }

                    await _responseBlock.SendAsync((responseId, response), _baseCancellationToken);
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

        private void StopReader(Exception exception)
        {
            Debug.WriteLine("Stopping reader");
            foreach (var (_, tcs) in _completeResponses.Where(tcs => tcs.Value.Task.Status == TaskStatus.WaitingForActivation))
            {
                var innerException = exception ?? new OperationCanceledException();
                Debug.WriteLine($"Set force {innerException.GetType()} in TaskCompletionSource in TaskStatus.WaitingForActivation");
                tcs.TrySetException(innerException);
            }

            Reader.CancelPendingRead();

            if (_tcpClient.Client.Connected)
                Reader.Complete(exception);

            Debug.WriteLine("Stopping reader end");
        }

        private void StopWriter(Exception exception)
        {
            Debug.WriteLine("Stopping writer");
            _requests.Complete();
            Writer.CancelPendingFlush();

            if (_tcpClient.Client.Connected)
                Writer.Complete(exception);

            Debug.WriteLine("Stopping writer end");
        }
    }
}