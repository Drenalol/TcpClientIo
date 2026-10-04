# TcpClientIo

Wrapper of [TcpClient](https://learn.microsoft.com/dotnet/api/system.net.sockets.tcpclient) that helps you focus on **WHAT** (declarative schema) you transfer over TCP, not **HOW** (imperative socket plumbing).

- Thread-safe
- Serialization with an attribute schema
- Big/Little endian support
- Async (built on `System.IO.Pipelines`)
- Cancellation support

[![NuGet](https://img.shields.io/nuget/vpre/TcpClientIo.svg)](https://www.nuget.org/packages/TcpClientIo/)
[![net10.0](https://img.shields.io/badge/net10.0-brightgreen)](https://learn.microsoft.com/dotnet/core/frameworks/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## How it works

Your TCP server is expected to speak a framing protocol with an application-level header (id, length, etc). You describe that frame with attributes on a plain class/struct, and the serializer/deserializer does everything else: reading from the socket, accumulating partial frames, matching responses to requests by id, and dispatching them to the waiting caller.

### Example byte array

| byte[] | 7B | 00 | 00 | 00 | 06 | 00 | 00 | 00 | 00 | D0 | 08 | A7 | 79 | 28 | B7 | 08 | A3 | 0B | 59 | 13 | 49 | 27 | 37 | 46 | B6 | D0 | 75 | A2 | EF | 07 | FA | 1F | 48 | 65 | 6C | 6C | 6F | 21 |
|--------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|

### Serialization of that frame

| Property name | Index | Length | Bytes                                                            | Value                                          | Reverse | Custom converter |
|---------------|-------|--------|------------------------------------------------------------------|------------------------------------------------|---------|------------------|
| Id            | 0     | 4      | [7B, 00, 00, 00]                                                 | 123                                            | false   | false            |
| * BodyLength  | 4     | 4      | [06, 00, 00, 00]                                                 | 6                                              | false   | false            |
| DateTime      | 8     | 8      | [00, D0, 08, A7, 79, 28, B7, 08]                                 | "1991-02-07 10:00:00" as DateTime              | false   | true             |
| Guid          | 16    | 16     | [A3, 0B, 59, 13, 49, 27, 37, 46, B6, D0, 75, A2, EF, 07, FA, 1F] | "13590ba3-2749-4637-b6d0-75a2ef07fa1f" as Guid | false   | true             |
| * Body        | 32    | 6      | [48, 65, 6C, 6C, 6F, 21]                                         | "Hello!" as string                             | false   | true             |

`* TcpDataType.Length and TcpDataType.Body are required together: if you set one, you must set the other.`

## Attribute schema

```c#
public class Request
{
    // TcpDataType.Id is optional; without it use the ReceiveAsync() overload without id
    [TcpData(0, 4, TcpDataType.Id)]
    public uint Id { get; set; }

    // TcpDataType.Length is mandatory when TcpDataType.Body is set.
    // Its value is overwritten by the serializer with the real body length,
    // including for value-type schemas.
    [TcpData(4, 4, TcpDataType.Length)]
    public uint BodyLength { get; set; }

    [TcpData(8, 8)]
    public DateTime DateTime { get; set; }

    [TcpData(16, 16)]
    public Guid Guid { get; set; }

    // TcpDataType.Body is mandatory when TcpDataType.Length is set
    [TcpData(32, TcpDataType = TcpDataType.Body)]
    public string Data { get; set; } = string.Empty;
}
```

Attribute properties:

| Property      | Meaning |
|---------------|---------|
| `Index`       | Property position in the byte array |
| `Length`      | Property length in bytes. Ignored (and overwritten) for `Body` |
| `TcpDataType` | Serialization rule: `MetaData` (default), `Id`, `Length`, `Body` |
| `Reverse`     | Reverses the byte sequence of the serialized property (for a peer with a different endianness) |

> `TcpDataType.Compose` exists in the API but is marked `[Obsolete(..., error: true)]` — the composed/nested schema feature is planned as a separate major feature and does not compile today.

## Usage

### Sending and receiving

```c#
using Drenalol.TcpClientIo.Client;
using Drenalol.TcpClientIo.Options;

// With an identifier type in the schema
var tcpClient = new TcpClientIo<uint, Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default);

// Or without an id (when the transport carries only Body and Length)
var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default);

var request = new Request
{
    // Serialized to [7B, 00, 00, 00]; with Reverse = true it would be [00, 00, 00, 7B]
    Id = 123U,

    // The serializer overwrites this with the real body length: [06, 00, 00, 00] for a 6-byte body
    BodyLength = 0,

    // Serialized with the custom DateTime converter: [00, D0, 08, A7, 79, 28, B7, 08]
    DateTime = DateTime.Parse("1991-02-07 10:00:00"),

    // Serialized with the custom Guid converter
    Guid = Guid.Parse("13590ba3-2749-4637-b6d0-75a2ef07fa1f"),

    // Serialized with the custom string converter: [48, 65, 6C, 6C, 6F, 21]
    Data = "Hello!"
};

// Send asynchronously
await tcpClient.SendAsync(request, CancellationToken.None);

// Receive the batch associated with the id. The id type must match the schema.
ITcpBatch<Response> resultBatch = await tcpClient.ReceiveAsync(123U, CancellationToken.None);

// If the schema has no TcpDataType.Id:
ITcpBatch<Response> resultBatch = await tcpClient.ReceiveAsync(CancellationToken.None);

// A batch supports iteration and LINQ
foreach (var response in resultBatch)
    Console.WriteLine(response.Data);

var response = resultBatch.First();

// Multiple responses with the same id are merged into one batch
var responses = await tcpClient.ReceiveAsync(id); // ITcpBatch<Response> with all of them

// Stop and clean up
await tcpClient.DisposeAsync();
```

### Consuming asynchronously

`GetConsumingAsyncEnumerable` works like a stream: it blocks until at least one batch is available and stops on cancellation. When the connection breaks it throws `TcpClientIoException` instead of hanging.

```c#
// One item = one batch
await foreach (ITcpBatch<Response> batch in tcpClient.GetConsumingAsyncEnumerable(CancellationToken.None))
{
    foreach (var response in batch)
    {
        // work with response
    }
}

// Or let the helper iterate the batches for you: one item = one response
await foreach (Response response in tcpClient.GetExpandableConsumingAsyncEnumerable(CancellationToken.None))
{
    // work with response
}
```

### TcpClientIo over an accepted TcpClient

Useful on the server side of a test listener or for a connect-then-wrap flow:

```c#
var listener = TcpListener.Create(10000);
listener.Start();

var tcpClient = await listener.AcceptTcpClientAsync();

var tcpClientIo = new TcpClientIo<uint, Request, Response>(tcpClient, TcpClientIoOptions.Default);
```

## Converters

The serializer uses stock `BitConverter` and supports 10 primitive types out of the box:

```c#
bool, char, double, short, int, long, float, ushort, uint, ulong
```

Plus special handling for `byte`, `byte[]` and `ReadOnlySequence<byte>` bodies.

For any other type write a converter and register it in the options. The package ships three converters (`TcpDateTimeConverter`, `TcpGuidConverter`, `TcpUtf8StringConverter`), but they are **not** registered automatically:

```c#
public class TcpUtf8StringConverter : TcpConverter<string>
{
    public override byte[] Convert(string input) => Encoding.UTF8.GetBytes(input);
    public override string ConvertBack(ReadOnlySpan<byte> input) => Encoding.UTF8.GetString(input);
}
```

```c#
var options = new TcpClientIoOptions
{
    Converters =
    [
        new TcpDateTimeConverter(),
        new TcpGuidConverter(),
        new TcpUtf8StringConverter()
    ]
};

var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, options);

// or one by one
options.RegisterConverter(new TcpUtf8StringConverter());
```

## Options

`TcpClientIoOptions.Default` gives sensible defaults for a `TcpClientIoOptions`:

| Option                    | Default      | Meaning |
|---------------------------|--------------|---------|
| `StreamPipeReaderOptions` | `bufferSize: 65536` | `System.IO.Pipelines` reader options for the `NetworkStream` |
| `StreamPipeWriterOptions` | default      | `System.IO.Pipelines` writer options for the `NetworkStream` |
| `TcpClientSendTimeout`    | 60000        | `TcpClient.SendTimeout`, ms |
| `TcpClientReceiveTimeout` | 60000        | `TcpClient.ReceiveTimeout`, ms |
| `PrimitiveValueReverse`   | false        | Reverse every primitive value by default (endianness of the peer) |
| `Converters`              | empty        | Custom `TcpConverter` registrations |
| `PipeExecutorOptions`     | `Default`    | `Logging` wraps pipe reads/writes with log statements (use with `LogLevel.Information`/`Debug`) |
| `IsRemoveRetainItems`     | false        | Drop responses that nobody ever received instead of retaining them |
| `RetainItemTtlMs`         | 0            | TTL for retained items when `IsRemoveRetainItems` is on |
| `UseSharedArrayPool`      | —            | `[Obsolete]` the shared `ArrayPool<byte>` is always used now |

## Observability

- `BytesWrite` / `BytesRead` — total bytes through the socket.
- `Requests` — requests queued for sending.
- `Waiters` — responses being awaited or ready to be received.
- `IsBroken` — read or write pipeline ended, or the socket dropped.
- Pass an `ILogger` to the constructor; `DisposeAsync` is idempotent.

## Dependencies

* [Nito.Disposables / AsyncEx](https://github.com/StephenCleary/AsyncEx)
* [WaitingDictionary](https://github.com/Drenalol/WaitingDictionary) — awaiting responses by id
* [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)
* `System.IO.Pipelines` and `System.Threading.Tasks.Dataflow` ship in the .NET 10 shared framework (no package dependency)

## Building

Prerequisite: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# build
dotnet build TcpClientIo.sln -c Release

# run the test suite (integration tests use a local echo listener; takes ~30 s)
dotnet test TcpClientIo.Tests

# benchmarks
dotnet run --project TcpClientIo.Benchmarks -c Release
```

## Publishing

The CI workflow (`.github/workflows/package-pack-push.yml`) builds and tests on pushes to `master`/`beta` and on PRs. On each push to `master` the `publish` job packs the library (version taken from `<PackageVersion>` in `TcpClientIo.Core.csproj`) and pushes it to two registries:

- **GitHub Packages** (`https://nuget.pkg.github.com/Drenalol/index.json`) using the built-in `GITHUB_TOKEN` — visible on the repository Packages page.
- **nuget.org** via **NuGet Trusted Publishing** (OIDC) — no long-lived API key is stored in the repository. The workflow requests a short-lived GitHub OIDC token, exchanges it for a temporary API key, and pushes.

To ship a new version: bump `<PackageVersion>`, commit and push to `master`.

One-time setup on [nuget.org → Trusted Publishing](https://www.nuget.org/account/TrustedPublishing):

1. Create a policy for the package `TcpClientIo`:
   - Repository owner: `Drenalol`
   - Repository: `TcpClientIo`
   - Workflow file: `package-pack-push.yml`
2. Add the repository secret `NUGET_USER` with your nuget.org **profile name** (not email).
3. After the first successful run, delete any old `NUGET_AUTH_TOKEN` secret and revoke legacy API keys.

Consumers of the GitHub Packages feed need a source and credentials configured:

```bash
dotnet nuget add source https://nuget.pkg.github.com/Drenalol/index.json \
  --name github --username <GITHUB_USER> --password <PERSONAL_ACCESS_TOKEN>
```

## License

[MIT](LICENSE)
