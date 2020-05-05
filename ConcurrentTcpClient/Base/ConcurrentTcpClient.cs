using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.Base
{
    public sealed class ConcurrentTcpClient : IDuplexPipe, IDisposable
    {
        private readonly CancellationTokenSource _internalCancellationTokenSource;
        private readonly CancellationToken _internalCancellationToken;
        private readonly BlockingCollection<byte[]> _packagesToSend;
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<TcpPackageExperiment>> _awaiters;
        private readonly TcpClient _tcpClient;
        private readonly object _lock = new object();
        private PipeReader Reader { get; set; }
        private PipeWriter Writer { get; set; }
        PipeReader IDuplexPipe.Input => Reader;
        PipeWriter IDuplexPipe.Output => Writer;

        /// <summary>
        /// WARNING! This method lock internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int Awaiters => _awaiters.Count;

        public int SendQueue => _packagesToSend.Count;

        public ConcurrentTcpClient(IPAddress address, int port, Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory, ConcurrentTcpClientOptions concurrentTcpClientOptions = null)
            : this(address, port, concurrentTcpClientOptions)
        {
            SetupTasks(readFactory);
        }

        public ConcurrentTcpClient(TcpClient tcpClient, Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory, ConcurrentTcpClientOptions concurrentTcpClientOptions = null)
            : this(tcpClient, concurrentTcpClientOptions)
        {
            SetupTasks(readFactory);
        }

        private ConcurrentTcpClient(TcpClient tcpClient, ConcurrentTcpClientOptions concurrentTcpClientOptions) : this()
        {
            _tcpClient = tcpClient;
            SetupTcpClient(concurrentTcpClientOptions);
        }

        private ConcurrentTcpClient(IPAddress address, int port, ConcurrentTcpClientOptions concurrentTcpClientOptions) : this()
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(address, port);
            SetupTcpClient(concurrentTcpClientOptions);
        }

        private ConcurrentTcpClient()
        {
            _internalCancellationTokenSource = new CancellationTokenSource();
            _internalCancellationToken = _internalCancellationTokenSource.Token;
            _packagesToSend = new BlockingCollection<byte[]>();
            _awaiters = new ConcurrentDictionary<uint, TaskCompletionSource<TcpPackageExperiment>>();
        }

        private void SetupTcpClient(ConcurrentTcpClientOptions concurrentTcpClientOptions)
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);
            var options = concurrentTcpClientOptions ?? ConcurrentTcpClientOptions.Default;
            Reader = PipeReader.Create(_tcpClient.GetStream(), options.StreamPipeReaderOptions);
            Writer = PipeWriter.Create(_tcpClient.GetStream(), options.StreamPipeWriterOptions);
        }

        private void SetupTasks(Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory)
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => TcpReadAsync(readFactory), TaskCreationOptions.LongRunning);
        }

        public bool TrySend(TcpPackage package, int timeout, CancellationToken? token = default) => InternalTrySend(package, timeout, token ?? _internalCancellationToken);

        public bool TrySend(TcpPackage package) => InternalTrySend(package, 0, new CancellationToken());

        public void Send(TcpPackage package, CancellationToken? token = default) => InternalTrySend(package, -1, token);

        private bool InternalTrySend(TcpPackage package, int timeout, CancellationToken? token = default)
        {
            try
            {
                if (package.PackageSize <= 0)
                    throw new ArgumentException("Package is empty");

                return !_packagesToSend.IsAddingCompleted && _packagesToSend.TryAdd(package.ToArray(), timeout, token ?? _internalCancellationToken);
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

        private TaskCompletionSource<TcpPackageExperiment> InternalGetOrAddLazyTcs(uint key) => _awaiters.GetOrAdd(key, _ => InternalCreateLazyTcs().Value);

        private Lazy<TaskCompletionSource<TcpPackageExperiment>> InternalCreateLazyTcs() => new Lazy<TaskCompletionSource<TcpPackageExperiment>>(() => new TaskCompletionSource<TcpPackageExperiment>());

        public async Task<TcpPackageExperiment> ReceiveAsync(uint packageId, CancellationToken? token = default) => await InternalReceiveAsync(packageId, token ?? _internalCancellationToken);

        private Task<TcpPackageExperiment> InternalReceiveAsync(uint packageId, CancellationToken token)
        {
            TaskCompletionSource<TcpPackageExperiment> tcs;
            // Info about lock read below on another lock
            lock (_lock)
            {
                if (!_awaiters.TryRemove(packageId, out tcs))
                    tcs = InternalGetOrAddLazyTcs(packageId);
            }

            using (token.Register(() =>
            {
                var cancelled = tcs.TrySetCanceled();
                Debug.WriteLine(cancelled ? $"Cancelled {packageId.ToString()}" : $"Not cancelled {packageId.ToString()}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return tcs.Task;
            }
        }

        private async Task TcpWriteAsync()
        {
            Exception innerException = null;

            try
            {
                foreach (var bytesArray in _packagesToSend.GetConsumingEnumerable())
                {
                    _internalCancellationToken.ThrowIfCancellationRequested();
                    await Writer.WriteAsync(bytesArray, _internalCancellationToken);
                    Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} -> PackageWrite {bytesArray.Length.ToString()} bytes");
                }
            }
            catch (OperationCanceledException canceledException)
            {
                innerException = canceledException;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"{nameof(TcpWriteAsync)} Got {exception.GetType()}, {exception.Message}");
                innerException = exception;
                await StopReader(exception);
                throw;
            }
            finally
            {
                await StopWriter(innerException);
            }
        }

        private async Task TcpReadAsync(Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory)
        {
            Exception innerException = null;

            try
            {
                while (true)
                {
                    _internalCancellationToken.ThrowIfCancellationRequested();
                    var package = await readFactory(Reader, _internalCancellationToken);
                    TcpPackageExperiment packageResult = null;

                    if (package == null)
                    {
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff}) <- package == null, waiters: {_awaiters.Count.ToString()}");
                        continue;
                    }

                    TaskCompletionSource<TcpPackageExperiment> tcs;
                    // From MSDN: ConcurrentDictionary<TKey,TValue> is designed for multithreaded scenarios.
                    // You do not have to use locks in your code to add or remove items from the collection.
                    // However, it is always possible for one thread to retrieve a value, and another thread
                    // to immediately update the collection by giving the same key a new value.
                    lock (_lock)
                    {
                        if (_awaiters.TryRemove(package.PackageId, out tcs))
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

                    void NewPackageResult(TaskCompletionSource<TcpPackageExperiment> innerTcs = null)
                    {
                        packageResult = new TcpPackageExperiment(package);
                        tcs = innerTcs ?? InternalGetOrAddLazyTcs(package.PackageId);
                        Debug.WriteLine(
                            $"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- PackageRead (tcs {(innerTcs == null ? "new" : "exists")}, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageResult.PackageId.ToString()}, PackageSize: {package.PackageSize.ToString()} bytes, PackageResult.QueueCount: {packageResult.QueueCount.ToString()}");
                    }

                    void ExistsPackageResult()
                    {
                        packageResult = tcs.Task.Result;
                        packageResult.Enqueue(package);
                        tcs = InternalGetOrAddLazyTcs(package.PackageId);
                        Debug.WriteLine(
                            $"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- PackageRead (tcs exists, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageResult.PackageId.ToString()}, PackageSize: {package.PackageSize.ToString()} bytes, PackageResult.QueueCount: {packageResult.QueueCount.ToString()}");
                    }

                    tcs.SetResult(packageResult);
                }
            }
            catch (OperationCanceledException canceledException)
            {
                innerException = canceledException;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"{nameof(TcpReadAsync)} Got {exception.GetType()}, {exception.Message}");
                innerException = exception;
                await StopWriter(exception);
                throw;
            }
            finally
            {
                await StopReader(innerException);
            }
        }

        private async Task StopReader(Exception exception)
        {
            Debug.WriteLine("Stopping reader");
            foreach (var (_, tcs) in _awaiters.Where(tcs => tcs.Value.Task.Status == TaskStatus.WaitingForActivation))
            {
                var innerException = exception ?? new OperationCanceledException();
                Debug.WriteLine($"Set force {innerException.GetType()} in TaskCompletionSource in TaskStatus.WaitingForActivation");
                tcs.TrySetException(innerException);
            }

            await Reader.CompleteAsync(exception);
            Reader.CancelPendingRead();
            Debug.WriteLine("Stopping reader end");
        }

        private async Task StopWriter(Exception exception)
        {
            Debug.WriteLine("Stopping writer");
            _packagesToSend.CompleteAdding();
            await Writer.CompleteAsync(exception);
            Writer.CancelPendingFlush();
            Debug.WriteLine("Stopping writer end");
        }

        public void Dispose()
        {
            Debug.WriteLine("Disposing");
            if (_internalCancellationTokenSource != null && !_internalCancellationTokenSource.IsCancellationRequested)
                _internalCancellationTokenSource.Cancel();

            _packagesToSend?.Dispose();
            _tcpClient?.Dispose();
            Debug.WriteLine("Disposing end");
        }
    }
}