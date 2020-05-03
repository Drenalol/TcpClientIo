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

        private readonly BlockingCollection<byte[]> _readyToSend;

        private readonly ConcurrentDictionary<uint, TaskCompletionSource<TcpPackageExperiment>> _readResults;
        //private ImmutableDictionary<uint, TaskCompletionSource<TcpPackageExperiment>> _immutableDictionary;

        //private ImmutableDictionary<uint, TaskCompletionSource<TcpPackage>> _immutableDictionary;
        private readonly TcpClient _tcpClient;

        private PipeReader Reader { get; set; }
        PipeReader IDuplexPipe.Input => Reader;
        private PipeWriter Writer { get; set; }
        PipeWriter IDuplexPipe.Output => Writer;

        /// <summary>
        /// WARNING! This method lock internal <see cref="ConcurrentDictionary{TKey,TValue}"/>, be careful of frequently use.
        /// </summary>
        public int ReadCount => _readResults.Count;

        public int SendQueue => _readyToSend.Count;

        public ConcurrentTcpClient(IPAddress address, int port, Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory, ConcurrentTcpClientOptions tcpClientAsyncOptions = null)
            : this(address, port, tcpClientAsyncOptions)
        {
            SetupTasks(readFactory);
        }

        public ConcurrentTcpClient(TcpClient tcpClient, Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory, ConcurrentTcpClientOptions tcpClientAsyncOptions = null)
            : this(tcpClient, tcpClientAsyncOptions)
        {
            SetupTasks(readFactory);
        }

        private ConcurrentTcpClient(TcpClient tcpClient, ConcurrentTcpClientOptions tcpClientAsyncOptions) : this()
        {
            _tcpClient = tcpClient;
            SetupTcpClient(tcpClientAsyncOptions);
        }

        private ConcurrentTcpClient(IPAddress address, int port, ConcurrentTcpClientOptions tcpClientAsyncOptions) : this()
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(address, port);
            SetupTcpClient(tcpClientAsyncOptions);
        }

        private ConcurrentTcpClient()
        {
            _internalCancellationTokenSource = new CancellationTokenSource();
            _internalCancellationToken = _internalCancellationTokenSource.Token;
            //_immutableDictionary = ImmutableDictionary<uint, TaskCompletionSource<TcpPackageExperiment>>.Empty;
            _readyToSend = new BlockingCollection<byte[]>();
            _readResults = new ConcurrentDictionary<uint, TaskCompletionSource<TcpPackageExperiment>>();
        }

        private void SetupTcpClient(ConcurrentTcpClientOptions tcpClientAsyncOptions)
        {
            if (!_tcpClient.Connected)
                throw new SocketException(10057);

            Reader = PipeReader.Create(_tcpClient.GetStream(), tcpClientAsyncOptions?.StreamPipeReaderOptions);
            Writer = PipeWriter.Create(_tcpClient.GetStream(), tcpClientAsyncOptions?.StreamPipeWriterOptions);
        }

        private void SetupTasks(Func<PipeReader, CancellationToken, Task<TcpPackage>> readFactory)
        {
            Task.Factory.StartNew(TcpWriteAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => TcpReadAsync(readFactory), TaskCreationOptions.LongRunning);
        }

        public bool TrySend(byte[] data, int timeout, CancellationToken? token = default) => InternalTrySend(data, timeout, token ?? _internalCancellationToken);

        public bool TrySend(byte[] data) => InternalTrySend(data, 0, new CancellationToken());

        public void Send(byte[] data, CancellationToken? token = default) => InternalTrySend(data, -1, token);

        private bool InternalTrySend(byte[] data, int timeout, CancellationToken? token = default)
        {
            try
            {
                return !_readyToSend.IsAddingCompleted && _readyToSend.TryAdd(data, timeout, token ?? _internalCancellationToken);
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

        public async Task<TcpPackageExperiment> ReceiveAsync(uint packageId, CancellationToken? token = default) => await InternalReceiveAsync(packageId, token ?? _internalCancellationToken);

        private Task<TcpPackageExperiment> InternalReceiveAsync(uint packageId, CancellationToken token)
        {
            if (!_readResults.TryRemove(packageId, out var tcs))
                tcs = _readResults.GetOrAdd(packageId, CreateLazyTcs);

            using (token.Register(() =>
            {
                var cancelled = tcs.TrySetCanceled();
                Debug.WriteLine(cancelled ? $"Cancelled {packageId.ToString()}" : $"Not cancelled {packageId.ToString()}, TaskStatus: {tcs.Task.Status.ToString()}");
            }))
            {
                return tcs.Task;
            }
        }

        private TaskCompletionSource<TcpPackageExperiment> CreateLazyTcs(uint key) => new Lazy<TaskCompletionSource<TcpPackageExperiment>>(() => new TaskCompletionSource<TcpPackageExperiment>(TaskCreationOptions.None)).Value;

        private async Task TcpWriteAsync()
        {
            Exception innerException = null;

            try
            {
                foreach (var bytesArray in _readyToSend.GetConsumingEnumerable())
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
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff}) <- package == null, waiters: {_readResults.Count.ToString()}");
                        continue;
                    }

                    if (_readResults.TryRemove(package.PackageId, out var tcs))
                    {
                        switch (tcs.Task.Status)
                        {
                            case TaskStatus.WaitingForActivation:
                                NewPackageResult(tcs);
                                break;
                            case TaskStatus.RanToCompletion:
                                await ExistsPackageResult();
                                break;
                        }
                    }
                    else
                        NewPackageResult();

                    void NewPackageResult(TaskCompletionSource<TcpPackageExperiment> innerTcs = null)
                    {
                        packageResult = new TcpPackageExperiment(package);
                        tcs = innerTcs ?? _readResults.GetOrAdd(package.PackageId, CreateLazyTcs);
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- PackageRead (tcs new, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageResult.PackageId.ToString()}, PackageSize: {package.PackageSize.ToString()} bytes, PackageResult.QueueCount: {packageResult.QueueCount.ToString()}");
                    }

                    async Task ExistsPackageResult()
                    {
                        packageResult = await tcs.Task;
                        packageResult.Enqueue(package);
                        tcs = _readResults.GetOrAdd(package.PackageId, CreateLazyTcs);
                        Debug.WriteLine($"[{Thread.CurrentThread.ManagedThreadId.ToString()}] {DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} <- PackageRead (tcs exists, TaskId: {tcs.Task.Id.ToString()}) PackageId: {packageResult.PackageId.ToString()}, PackageSize: {package.PackageSize.ToString()} bytes, PackageResult.QueueCount: {packageResult.QueueCount.ToString()}");
                    }

                    var setResult = tcs.TrySetResult(packageResult);
                    Debug.WriteLine($"{tcs.Task.Id.ToString()}:{setResult.ToString()}");
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
            foreach (var (_, tcs) in _readResults.Where(tcs => tcs.Value.Task.Status == TaskStatus.WaitingForActivation))
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
            _readyToSend.CompleteAdding();
            await Writer.CompleteAsync(exception);
            Writer.CancelPendingFlush();
            Debug.WriteLine("Stopping writer end");
        }

        public void Dispose()
        {
            Debug.WriteLine("Disposing");
            if (_internalCancellationTokenSource != null && !_internalCancellationTokenSource.IsCancellationRequested)
                _internalCancellationTokenSource.Cancel();

            _readyToSend?.Dispose();
            _tcpClient?.Dispose();
            Debug.WriteLine("Disposing end");
        }
    }
}