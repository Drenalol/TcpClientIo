using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Base;

namespace Drenalol.Client
{
    public sealed class TcpClientIo<TRequest, TResponse> : IDuplexPipe, IAsyncDisposable where TResponse : new()
    {
        private readonly TcpPackageSerializer<TRequest, TResponse> _serializer;
        private readonly CancellationTokenSource _baseCancellationTokenSource;
        private readonly CancellationToken _baseCancellationToken;
        private readonly BlockingCollection<byte[]> _packagesToSend;
        private readonly ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>> _waiters;
        private readonly TcpClient _tcpClient;
        private readonly SemaphoreSlim _semaphore;
        private Exception _internalException;
        private readonly object _lock = new object();
        private PipeReader Reader { get; set; }
        private PipeWriter Writer { get; set; }
        PipeReader IDuplexPipe.Input => Reader;
        PipeWriter IDuplexPipe.Output => Writer;

        /// <summary>
        /// WARNING! This property lock internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Waiters => _waiters.Count;

        public int SendQueue => _packagesToSend.Count;

        public TcpClientIo(TcpClient tcpClient, TcpClientIoOptions tcpClientIoOptions = null) : this()
        {
            _tcpClient = tcpClient;
            SetupTcpClient(tcpClientIoOptions);
            SetupTasks();
        }

        public TcpClientIo(IPAddress address, int port, TcpClientIoOptions tcpClientIoOptions = null) : this()
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(address, port);
            SetupTcpClient(tcpClientIoOptions);
            SetupTasks();
        }

        private TcpClientIo()
        {
            _baseCancellationTokenSource = new CancellationTokenSource();
            _baseCancellationToken = _baseCancellationTokenSource.Token;
            _packagesToSend = new BlockingCollection<byte[]>();
            _waiters = new ConcurrentDictionary<object, TaskCompletionSource<TcpPackageBatch<TResponse>>>();
            _serializer = new TcpPackageSerializer<TRequest, TResponse>();
            _semaphore = new SemaphoreSlim(2, 2);
        }

        private void SetupTcpClient(TcpClientIoOptions tcpClientIoOptions)
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);

            var options = tcpClientIoOptions ?? TcpClientIoOptions.Default;
            Reader = PipeReader.Create(_tcpClient.GetStream(), options.StreamPipeReaderOptions);
            Writer = PipeWriter.Create(_tcpClient.GetStream(), options.StreamPipeWriterOptions);
        }

        private void SetupTasks()
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(TcpReadAsync, TaskCreationOptions.LongRunning);
        }

        public bool TrySend(TRequest request, int timeout, CancellationToken? token = default) => InternalTrySend(request, timeout, token ?? _baseCancellationToken);

        public bool TrySend(TRequest request) => InternalTrySend(request, 0, new CancellationToken());

        public void Send(TRequest request, CancellationToken? token = default) => InternalTrySend(request, -1, token);

        private bool InternalTrySend(TRequest request, int timeout, CancellationToken? token = default)
        {
            try
            {
                var serializedRequest = _serializer.Serialize(request);
                return !_packagesToSend.IsAddingCompleted && _packagesToSend.TryAdd(serializedRequest, timeout, token ?? _baseCancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Got {e.GetType()}: {e.Message}");
                throw;
            }
        }

        private TaskCompletionSource<TcpPackageBatch<TResponse>> InternalGetOrAddLazyTcs(object key) => _waiters.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);

        private Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<TcpPackageBatch<TResponse>>>(() => new TaskCompletionSource<TcpPackageBatch<TResponse>>());

        public async Task<TcpPackageBatch<TResponse>> ReceiveAsync(object packageId, CancellationToken? token = default) => await InternalReceiveAsync(packageId, token ?? _baseCancellationToken);

        private Task<TcpPackageBatch<TResponse>> InternalReceiveAsync(object packageId, CancellationToken token)
        {
            TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
            // Info about lock read below in TcpReadAsync method
            lock (_lock)
            {
                if (!_waiters.TryRemove(packageId, out tcs))
                    tcs = InternalGetOrAddLazyTcs(packageId);
            }

            using (token.Register(() =>
            {
                var cancelled = tcs.TrySetCanceled();
                Debug.WriteLine(cancelled ? $"Cancelled {packageId}" : $"Not cancelled {packageId}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return tcs.Task;
            }
        }

        private async Task TcpWriteAsync()
        {
            await _semaphore.WaitAsync(_baseCancellationToken);
            
            try
            {
                foreach (var bytesArray in _packagesToSend.GetConsumingEnumerable(_baseCancellationToken))
                {
                    _baseCancellationToken.ThrowIfCancellationRequested();
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
                    TcpPackageBatch<TResponse> packageBatch = null;

                    if (response == null)
                    {
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff}) <- package == null, waiters: {_waiters.Count.ToString()}");
                        continue;
                    }

                    TaskCompletionSource<TcpPackageBatch<TResponse>> tcs;
                    // From MSDN: ConcurrentDictionary<TKey,TValue> is designed for multithreaded scenarios.
                    // You do not have to use locks in your code to add or remove items from the collection.
                    // However, it is always possible for one thread to retrieve a value, and another thread
                    // to immediately update the collection by giving the same key a new value.
                    lock (_lock)
                    {
                        if (_waiters.TryRemove(responseId, out tcs))
                        {
                            switch (tcs.Task.Status)
                            {
                                case TaskStatus.WaitingForActivation:
                                    NewPackageResult(tcs);
                                    break;
                                case TaskStatus.RanToCompletion:
                                    ExistsPackageResult();
                                    break;
                            }
                        }
                        else
                            NewPackageResult();
                    }

                    void NewPackageResult(TaskCompletionSource<TcpPackageBatch<TResponse>> innerTcs = null)
                    {
                        packageBatch = new TcpPackageBatch<TResponse>(responseId, response);
                        tcs = innerTcs ?? InternalGetOrAddLazyTcs(responseId);
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                        $"<- PackageRead (tcs {(innerTcs == null ? "new" : "exists")}, TaskId: {tcs.Task.Id.ToString()}) " +
                                        $"PackageId: {packageBatch.PackageId}, PackageResult.QueueCount: {packageBatch.QueueCount.ToString()}");
                    }

                    void ExistsPackageResult()
                    {
                        packageBatch = tcs.Task.Result;
                        packageBatch.Enqueue(response);
                        tcs = InternalGetOrAddLazyTcs(responseId);
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} " +
                                        $"<- PackageRead (tcs exists, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageBatch.PackageId}, " +
                                        $"PackageResult.QueueCount: {packageBatch.QueueCount.ToString()}");
                    }

                    tcs.SetResult(packageBatch);
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

        /*private async Task MonitorAsync()
        {
            while (_semaphore.CurrentCount < 2)
            {
                await Task.Delay(100, CancellationToken.None);
            }

            if (!_baseCancellationToken.IsCancellationRequested)
                throw TcpClientIoException.Throw(TcpClientIoTypeException.InternalError, _internalException?.ToString());
        }*/

        private void StopReader(Exception exception)
        {
            Debug.WriteLine("Stopping reader");
            foreach (var (_, tcs) in _waiters.Where(tcs => tcs.Value.Task.Status == TaskStatus.WaitingForActivation))
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
            _packagesToSend.CompleteAdding();
            Writer.CancelPendingFlush();
            
            if (_tcpClient.Client.Connected)
                Writer.Complete(exception);
            
            Debug.WriteLine("Stopping writer end");
        }

        public async ValueTask DisposeAsync()
        {
            Debug.WriteLine("Disposing");
            
            if (_baseCancellationTokenSource != null && !_baseCancellationTokenSource.IsCancellationRequested)
                _baseCancellationTokenSource.Cancel();

            var i = 0;
            while (i++ < 60 && _semaphore.CurrentCount < 2)
            {
                await Task.Delay(100, CancellationToken.None);
            }
            
            _packagesToSend?.Dispose();
            _tcpClient?.Dispose();
            _semaphore?.Dispose();
            Debug.WriteLine("Disposing end");
        }
    }
}