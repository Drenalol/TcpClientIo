using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using Drenalol.TcpClientIo.Client;
using Drenalol.TcpClientIo.Contracts;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Emulator;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Stuff;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Drenalol.TcpClientIo;

[TestFixture(TestOf = typeof(TcpClientIo<,>))]
public class TcpClientIoTests : UseTcpListenerTest
{
    public static readonly IPAddress IpAddress = IPAddress.Any;

    private static readonly ConcurrentDictionary<LogLevel, (TcpClientIoOptions Options, ILoggerFactory LoggerFactory)> DefaultsCache = new();

    private static (TcpClientIoOptions, ILoggerFactory) GetDefaults(LogLevel logLevel) =>
        DefaultsCache.GetOrAdd(
            logLevel,
            static level =>
            {
                var options = TcpClientIoOptions.Default;

                options.Converters = [new TcpGuidConverter(), new TcpDateTimeConverter(), new TcpUtf8StringConverter()];

                options.StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 10240000);
                options.StreamPipeWriterOptions = new StreamPipeWriterOptions();
                options.PipeExecutorOptions = PipeExecutor.Logging;

                var loggerFactory = LoggerFactory.Create(lb =>
                {
                    lb.SetMinimumLevel(level);
                    lb.AddDebug();
                    lb.AddConsole();
                });

                return (options, loggerFactory);
            }
        );

    public TcpClientIo<TId, T, TR> GetClient<TId, T, TR>(int? port = null, LogLevel logLevel = LogLevel.Warning) where TR : new() where TId : struct where T : notnull
    {
        var (options, loggerFactory) = GetDefaults(logLevel);
        return new TcpClientIo<TId, T, TR>(IpAddress, port ?? EmulatorPort, options, loggerFactory.CreateLogger<TcpClientIo<T, TR>>());
    }

    public TcpClientIo<T, TR> GetClient<T, TR>(int? port = null, LogLevel logLevel = LogLevel.Warning) where TR : new() where T : notnull
    {
        var (options, loggerFactory) = GetDefaults(logLevel);
        return new TcpClientIo<T, TR>(IpAddress, port ?? EmulatorPort, options, loggerFactory.CreateLogger<TcpClientIo<T, TR>>());
    }

    [Test]
    public async Task SingleSendReceiveTest()
    {
        await using var tcpClient = GetClient<long, Mock, Mock>(logLevel: LogLevel.Debug);
        var request = Mock.Default();
        await tcpClient.SendAsync(request);
        var batch = await tcpClient.ReceiveAsync(1337L);
        var response = batch.First();
        Assert.That(response, Is.EqualTo(request));
        await tcpClient.DisposeAsync();
        Assert.That(tcpClient.IsBroken);
    }

    [Test]
    public async Task SingleByteAndByteArrayTest()
    {
        await using var tcpClient = GetClient<int, MockByteBody, MockByteBody>();

        var mock = new MockByteBody
        {
            Id = 1,
            Body = "TestHello",
            TestByte = 123,
            TestByteArray = [123, 124]
        };

        await tcpClient.SendAsync(mock);
        var batch = await tcpClient.ReceiveAsync(1);
        Assert.That(batch.Single().Body, Is.EqualTo("TestHello"));

        await tcpClient.DisposeAsync();
        Assert.That(tcpClient.IsBroken);
    }

    [Test]
    public async Task MockBodyInSequenceTest()
    {
        await using var tcpClient = GetClient<int, MockMemoryBody, MockMemoryBody>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await Parallel.ForEachAsync(Enumerable.Range(1, 1000), cts.Token, SendAsync);
        cts.Dispose();
        Assert.That(tcpClient.BytesRead, Is.EqualTo(tcpClient.BytesWrite));
        Assert.That(cts.IsCancellationRequested, Is.False);

        async ValueTask SendAsync(int id, CancellationToken cancellationToken)
        {
            var bytes = new byte[TestContext.CurrentContext.Random.Next(1024 * 32)];
            TestContext.CurrentContext.Random.NextBytes(bytes);
            var mock = new MockMemoryBody
            {
                Id = id,
                TestByte = 123,
                TestByteArray = [111, 222],
                Body = bytes.ToSequence()
            };

            await tcpClient.SendAsync(mock, cancellationToken);
            var response = (await tcpClient.ReceiveAsync(id, cancellationToken)).Single();

            try
            {
                Assert.That(response, Is.EqualTo(mock));
            }
            catch
            {
                // ReSharper disable once AccessToDisposedClosure
                cts.Cancel();
            }
        }
    }

    [TestCase(1000, 1, 5)]
    [TestCase(1000, 4, 5)]
    public async Task MultipleConsumersAsyncTest(int requests, int consumers, double timeout)
    {
        var requestsPerConsumer = requests / consumers;
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(timeout));
        var consumersList = Enumerable.Range(0, consumers).Select(_ => GetClient<long, Mock, Mock>()).ToList();
        var requestQueue = 0;
        var waitersQueue = 0;
        var bytesWrite = 0L;
        var bytesRead = 0L;
        var sended = 0;
        var received = 0;

        await Task.WhenAll(consumersList.Select(io => Task.Run(() => DoWork(io), cts.Token)).ToArray());

        foreach (var io in consumersList)
            await io.DisposeAsync();

        async Task DoWork(ITcpClientIo<long, Mock, Mock> tcpClient)
        {
            try
            {
                var tasks = Enumerable.Range(0, requestsPerConsumer).Select(i => (long)i).Select(SendAsync)
                    .Concat(Enumerable.Range(0, requestsPerConsumer).Select(i => (long)i).Select(ReceiveAsync))
                    .ToList();

                await Task.WhenAll(tasks);

                async Task SendAsync(long id)
                {
                    var mock = Mock.Default(id);
                    await tcpClient.SendAsync(mock, cts.Token);
                    Interlocked.Increment(ref sended);
                }

                async Task ReceiveAsync(long id)
                {
                    var batch = await tcpClient.ReceiveAsync(id, cts.Token);
                    var mock = batch.First();
                    Assert.That(mock.Size, Is.EqualTo(mock.Data.Length));
                    Interlocked.Increment(ref received);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                Interlocked.Add(ref bytesWrite, tcpClient.BytesWrite);
                Interlocked.Add(ref bytesRead, tcpClient.BytesRead);
                Interlocked.Add(ref requestQueue, tcpClient.Requests);
                Interlocked.Add(ref waitersQueue, tcpClient.Waiters);
            }
        }

        TestContext.WriteLine($"Requests: {requestQueue}");
        TestContext.WriteLine($"Waiters: {waitersQueue}");
        TestContext.WriteLine($"Sent: {sended}");
        TestContext.WriteLine($"Received: {received}");
        TestContext.WriteLine($"BytesWrite: {Math.Round(bytesWrite / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
        TestContext.WriteLine($"BytesRead: {Math.Round(bytesRead / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
    }

    [TestCase(1000, true)]
    [TestCase(1000, false)]
    public async Task ConsumingAsyncEnumerableTest(int requests, bool expandBatch)
    {
        var sended = 0;
        var received = 0;
        using var cts = new CancellationTokenSource();
        await using ITcpClientIo<long, Mock, Mock> tcpClient = GetClient<long, Mock, Mock>();

        var sendTasks = Enumerable.Range(0, requests).Select(
            async i =>
            {
                var mock = Mock.Default(expandBatch ? 0 : i);
                await tcpClient.SendAsync(mock, cts.Token);
                Interlocked.Increment(ref sended);
            }
        ).ToArray();
        _ = Task.WhenAll(sendTasks).ContinueWith(t => TestContext.WriteLine(t.Exception?.GetBaseException()), TaskContinuationOptions.OnlyOnFaulted);

        _ = Task.Run(
            async () =>
            {
                while (received < requests && !cts.IsCancellationRequested)
                {
                    await Task.Delay(1, cts.Token);
                }

                cts.Cancel();
            },
            cts.Token
        );

        try
        {
            if (expandBatch)
            {
                await foreach (var _ in tcpClient.GetExpandableConsumingAsyncEnumerable(cts.Token))
                {
                    Interlocked.Increment(ref received);
                }
            }
            else
            {
                await foreach (var _ in tcpClient.GetConsumingAsyncEnumerable(cts.Token))
                {
                    Interlocked.Increment(ref received);
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            TestContext.WriteLine(e);
        }
        finally
        {
            TestContext.WriteLine($"Requests: {tcpClient.Requests}");
            TestContext.WriteLine($"Waiters: {tcpClient.Waiters}");
            TestContext.WriteLine($"Sent: {sended}");
            TestContext.WriteLine($"Received: {received}");
            TestContext.WriteLine($"BytesWrite: {Math.Round(tcpClient.BytesWrite / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
            TestContext.WriteLine($"BytesRead: {Math.Round(tcpClient.BytesRead / 1024000.0, 2).ToString(CultureInfo.CurrentCulture)} MegaBytes");
        }
    }

    [Test]
    public async Task NoIdTest()
    {
        await using var client = GetClient<MockNoId, MockNoId>();
        var mock = new MockNoId
        {
            Body = "Qwerty!"
        };
        await client.SendAsync(mock);
        var batch = await client.ReceiveAsync();
        var mockNoId = batch.First();
        Assert.That(mockNoId.Size, Is.EqualTo(mock.Size));
        Assert.That(mockNoId.Body, Is.EqualTo("Qwerty!"));
    }

    [Test]
    public async Task NoIdNoBodyTest()
    {
        await using var client = GetClient<MockOnlyMetaData, MockOnlyMetaData>();
        var mock = new MockOnlyMetaData
        {
            Test = 1337,
            Long = 777788889999
        };
        await client.SendAsync(mock);
        var batch = await client.ReceiveAsync();
        var mockNoId = batch.First();
        Assert.That(mockNoId.Test, Is.EqualTo(mock.Test));
        Assert.That(mockNoId.Long, Is.EqualTo(mock.Long));
    }

    [Test]
    public async Task SameIdTest()
    {
        const int requests = 10;
        var list = new List<int>();
        var count = 0;
        var error = 0;

        await using var tcpClient = GetClient<long, Mock, Mock>();

        var sendAll = Task.Run(
            async () => await Parallel.ForEachAsync(
                Enumerable.Range(0, requests),
                async (_, token) =>
                {
                    var mock = Mock.Default(0);

                    try
                    {
                        await tcpClient.SendAsync(mock, token);
                    }
                    catch
                    {
                        Interlocked.Increment(ref error);
                    }
                }
            )
        );

        while (count < requests)
        {
            var delay = TestContext.CurrentContext.Random.Next(1, 200);
            await Task.Delay(delay);

            if (error > 0)
                throw new Exception("Parallel.For has errors");

            var packageResult = await tcpClient.ReceiveAsync(0);
            Assert.That(packageResult, Is.Not.Null);
            var queue = packageResult.Count;
            count += queue;

            list.AddRange(packageResult.Select(mock => mock.Size));

            TestContext.WriteLine($"({count}/{requests}) +{queue}, by {delay} ms, SendQueue: {tcpClient.Requests}, ReadCount: {tcpClient.Waiters}");
        }

        await sendAll;

        var havingCount = list.GroupBy(u => u).Where(p => p.Count() > 1).Aggregate("", (acc, next) => $"{next.Key}, {acc}");
        TestContext.WriteLine($"Non-UNIQ Sizes: {havingCount}");

        await tcpClient.DisposeAsync();
        Assert.That(tcpClient.IsBroken);
    }

    [Test]
    public async Task DisposeTest()
    {
        var tcpClient = GetClient<long, Mock, Mock>();
        var dispose = Task.Delay(3000).ContinueWith(_ => tcpClient.DisposeAsync().AsTask());
        var mock = Mock.Default();
        while (true)
        {
            try
            {
                await tcpClient.SendAsync(mock);
                await tcpClient.ReceiveAsync(mock.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got Exception: {e.GetType()}: {e}");
                Assert.That(e, Is.InstanceOf<OperationCanceledException>().Or.InstanceOf<ObjectDisposedException>());
                Assert.That(tcpClient.IsBroken);
                break;
            }
        }

        await dispose.Unwrap();
    }

    [Test]
    public async Task CancelSendReceiveTest()
    {
        await using var tcpClient = GetClient<long, Mock, Mock>();
        var mock = Mock.Default();
        var attempts = 0;
        while (attempts < 3)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            while (true)
            {
                try
                {
                    await tcpClient.SendAsync(mock, cts.Token);
                    await tcpClient.ReceiveAsync(mock.Id, cts.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Got Exception: {e.GetType()}: {e}");
                    Assert.That(e, Is.InstanceOf<OperationCanceledException>());
                    Assert.That(tcpClient.IsBroken, Is.False);
                    attempts++;
                    break;
                }
            }
        }
    }

    [Test]
    public async Task EmptyBodyTest()
    {
        await using var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>();
        var mock = new MockNoIdEmptyBody { Length = 0, Empty = "" };
        await client.SendAsync(mock);
        var batch = await client.ReceiveAsync(default);
        Assert.That(batch, Is.Not.Null);
        Assert.That(batch.Single().Empty, Is.Empty);
    }

    [Test]
    public async Task ReceiveAndListenerDisconnectTest()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var emulator = ListenerEmulator.Create(cts.Token, ListenerEmulatorConfig.Default);
        await using var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: emulator.Port);

        await Assert.ThrowsAsync<TcpClientIoException>(async () => await client.ReceiveAsync(0, CancellationToken.None));
        Assert.That(client.IsBroken);
    }

    [Test]
    public async Task ReceiveAndCancelTaskTest()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var emulator = ListenerEmulator.Create(cts.Token, ListenerEmulatorConfig.Default);
        await using var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: emulator.Port);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<TaskCanceledException>(() => client.ReceiveAsync(0, cts2.Token));
        Assert.That(client.IsBroken, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ConsumeAndDisconnectTest(bool ownToken)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var emulator = ListenerEmulator.Create(cts.Token, ListenerEmulatorConfig.Default);
        await using var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: emulator.Port);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await foreach (var _ in client.GetExpandableConsumingAsyncEnumerable(ownToken ? cts2.Token : default))
            {
            }
            Assert.Fail("Expected TcpClientIoException");
        }
        catch (Exception e)
        {
            Assert.That(e, Is.InstanceOf<TcpClientIoException>());
            Assert.That(client.IsBroken);
        }
    }

    [Test]
    public async Task ListenerDisconnectTest()
    {
        using var cts = new CancellationTokenSource();
        var emulator = ListenerEmulator.Create(cts.Token, ListenerEmulatorConfig.Default);
        await using var client = GetClient<int, MockNoIdEmptyBody, MockNoIdEmptyBody>(port: emulator.Port);

        // prove the emulator accepted the connection before cancelling: cancelling earlier
        // races with AcceptTcpClientAsync and Stop() then leaves our socket open in the OS backlog
        await client.SendAsync(new MockNoIdEmptyBody());
        await client.ReceiveAsync(0);

        cts.Cancel();

        var sw = Stopwatch.StartNew();
        TcpClientIoException? sendError = null;

        while (sendError is null && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            try
            {
                await client.SendAsync(new MockNoIdEmptyBody(), CancellationToken.None);
                await Task.Delay(50);
            }
            catch (TcpClientIoException e)
            {
                sendError = e;
            }
        }

        Assert.That(sendError, Is.Not.Null);
        Assert.That(client.IsBroken);
    }
}
