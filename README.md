# TcpClientIo
Wrapper of [TcpClient](https://github.com/dotnet/runtime/blob/c7a246c000747ec728ac862b7a503348b103df0e/src/libraries/System.Net.Sockets/src/System/Net/Sockets/TCPClient.cs "Source Code dotnet/corefx/TCPClient.cs") what help focus on **WHAT** (Declarative) you transfer over TCP not **HOW** (Imperative)

- Thread-safe
- Serialization with attribute schema
- Big/Little endian
- Async
- Cancellation support

[![NuGet Pre Release](https://img.shields.io/nuget/vpre/TcpClientIo.svg?style=for-the-badge&logo=appveyor)](https://www.nuget.org/packages/TcpClientIo/)
[![netstandard 2.1](https://img.shields.io/badge/netstandard-2.1-brightgreen.svg?style=for-the-badge&logo=appveyor)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) 
## Documentation
#### Prerequisites
Your TCP Server accepts and send messages with application-level header (id, length, etc)

##### Example byte array
| byte[] | 7B | 00 | 00 | 00 | 06 | 00 | 00 | 00 | 00 | D0 | 08 | A7 | 79 | 28 | B7 | 08 | A3 | 0B | 59 | 13 | 49 | 27 | 37 | 46 | B6 | D0 | 75 | A2 | EF | 07 | FA | 1F | 48 | 65 | 6C | 6C | 6F | 21 |
|--------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
##### Serialization process
| Property name | Index | Length | Bytes                                                            | Value                                          | Reverse  | Custom converter |
|---------------|--------|--------|------------------------------------------------------------------|------------------------------------------------|---------|------------------|
| Id            | 0      | 4      | [7B, 00, 00, 00]                                                 | 123                                            | false   | false            |
| * BodyLength  | 4      | 4      | [06, 00, 00, 00]                                                 | 6                                              | false   | false            |
| DateTime      | 8      | 8      | [00, D0, 08, A7, 79, 28, B7, 08]                                 | "1991-02-07 10:00:00" as DateTime              | false   | true             |
| Guid          | 16     | 16     | [A3, 0B, 59, 13, 49, 27, 37, 46, B6, D0, 75, A2, EF, 07, FA, 1F] | "13590ba3-2749-4637-b6d0-75a2ef07fa1f" as Guid | false   | true             |
| * Body        | 32     | 6      | [48, 65, 6C, 6C, 6F, 21]                                         | "Hello!" as string                             | false   | true             |
`* Mandatory if at least one is set.`
#### Examples
###### Example #1. Sending and receiving.
```c#
// Creating TcpClientIo instance with schema of request/response and uint ID type
var tcpClient = new TcpClientIo<uint, Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default);
// Or without ID type (if your transport does'nt have id, only Body and Length)
var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default);

// Creating request
Request request = new Request
{
    // Serialized to [7B, 00, 00, 00], but if we set force reverse = true,
    // it will serialized to [00, 00, 00, 7B] (My arch is Little-Endian)
    Id = 123U, 
    
    // The serializer will take the length of the TcpTypeBody and overwrite the value.
    // Serialized to [06, 00, 00, 00] because the Data property has a length = 6
    Length = 0,

    // Will be used custom converter DateTime (about converters, read below)
    // Serialized to [00, D0, 08, A7, 79, 28, B7, 08]
    DateTime = DateTime.Parse("1991-02-07 10:00:00"),

    // Will be used custom converter Guid
    // Serialized to [A3, 0B, 59, 13, 49, 27, 37, 46, B6, D0, 75, A2, EF, 07, FA, 1F]
    Guid = Guid.Parse("13590ba3-2749-4637-b6d0-75a2ef07fa1f"),
    
    // Will be used custom converter string
    // Serialized to [48, 65, 6C, 6C, 6F, 21]
    Data = "Hello!"
};

// Send request asynchronously
await tcpClient.SendAsync(request, CancellationToken.None);

// Receive response in overtype ITcpBatch<Response> by identifier asynchronously.
// Identifier is strongly-typed, you must use the type specified in the request.
ITcpBatch<Response> resultBatch = await tcpClient.ReceiveAsync(123U, CancellationToken.None);

// Or if schema does not have TcpDataType.Id (Available from 1.0.9)
ITcpBatch<Response> resultBatch = await tcpClient.ReceiveAsync(CancellationToken.None);

// Batch support iteration
foreach (var response in resultBatch)
{
    // Hello!
    Console.WriteLine(response.Data);
}
// and LINQ queries
var response = resultBatch.First();
// Hello!
Console.WriteLine(response.Data);

// Check result
Assert.AreEqual(request.Id, response.Id);
Assert.AreEqual(request.BodyLength, response.BodyLength);
Assert.AreEqual(request.Data, response.Data);

//Stop & Cleanup
await tcpClient.DisposeAsync();
```
###### Example #2. Consuming asynchronously. (Available from 1.0.9, netstandard2.1 only)
```c#
// GetConsumingAsyncEnumerable works like a stream and will be stopped by cancellation when necessary.
await foreach (ITcpBatch<Response> batch in tcpClient.GetConsumingAsyncEnumerable(CancellationToken.None))
{
    // manual iterate batch
    foreach (var response in batch)
    {
        // work with response
    }
}
// or we can use Expandable method (will iterate batch for us inside)
await foreach (Response response in tcpClient.GetExpandableConsumingAsyncEnumerable(CancellationToken.None))
{
    // work with response
}
```
###### Example #3. TcpClient from TcpListener. (Available from 1.0.9, netstandard2.1 only)
```c#
// Suppose you have a listener.
var listener = TcpListener.Create(10000);
listener.Start();

// Get TcpClient instance
var tcpClient = await _listener.AcceptTcpClientAsync();

// Create TcpClientIo and pass it TcpClient
var tcpClientIo = new TcpClientIo<uint, Request, Response>(tcpClient, TcpClientIoOptions.Default);

//Start consuming from TcpClientIo
await foreach (ITcpBatch<uint, Request, Response> batch in tcpClientIo.GetConsumingAsyncEnumerable(CancellationToken.None))
{
    foreach (var response in batch)
    {
        // work
    }
}
```
###### Example #4. Compose properties. (Available from 1.4.0)
```c#
// RecursiveMock
public class RecursiveMock<T>
{
    [TcpData(0, 4, TcpDataType.Length)]  
    public int Length { get; set; }

    [TcpData(4, TcpDataType = TcpDataType.Compose)]
    public T Data { get; set; }
}

// Creating TcpClientIo instance with schema with generics
var tcpClient = new TcpClientIo<RecursiveMock<RecursiveMock<RecursiveMock<long>>>, RecursiveMock<RecursiveMock<RecursiveMock<long>>>>(IPAddress.Any, 10000, TcpClientIoOptions.Default);

// Compose RecursiveMock
var request = new RecursiveMock<RecursiveMock<RecursiveMock<long>>>();

// Send request asynchronously
await tcpClient.SendAsync(request, CancellationToken.None);

// Receive response
var response = await tcpClient.ReceiveAsync(CancellationToken.None).Single();

// Check data
Assert.NotNull(response); // check RecursiveMock
Assert.NotNull(response.Data); // check RecursiveMock.RecursiveMock
Assert.NotNull(response.Data.Data); // check RecursiveMock.RecursiveMock.RecursiveMock
Assert.IsInstanceOf<long>(response.Data.Data.Data); // check RecursiveMock.RecursiveMock.RecursiveMock.long
```
#### Attribute schema
Properties

`Index` Property position in Byte Array.

`Length` Property length in bytes. (If TcpDataType set to TcpDataType.Body or TcpDataType.Compose, is ignored and will be overwritten by the serializer.)

`TcpDataType` Sets the serialization rule for this property. Available: `MetaData` (default), `Id`, `Length`, `Body`, `Compose`.

`Reverse` Reverses the sequence of the bytes from serialized property (used for cases where the receiving side uses a different endianness.)
##### Example: Without generics
Request with first 32 bytes header, and body
```c#
public class Request
{
    // TcpDataType.Id not mandatory from 1.0.9
    [TcpData(0, 4, TcpDataType.Id)]
    public uint Id { get; set; }

    // TcpDataType.Length mandatory if TcpDataType.Body set
    [TcpData(4, 4, TcpDataType.Length)]
    public uint BodyLength { get; set; }

    [TcpData(8, 8)]
    public DateTime DateTime { get; set; }

    [TcpData(16, 16)]
    public Guid Guid { get; set; }

    // TcpDataType.Body mandatory if TcpDataType.Length set
    [TcpData(32, TcpDataType = TcpDataType.Body)]
    public string Data { get; set; }
}
```
##### Example: With generics (Available from 1.4.0)
```c#
public class RecursiveMock<T>
{
    // TcpDataType.Length mandatory if TcpDataType.Compose set
    [TcpData(0, 4, TcpDataType.Length)]  
    public int Length { get; set; }
    
    // TcpDataType.Compose mandatory if TcpDataType.Length set
    // Supports: Class, Sctruct, Primitive Types
    [TcpData(4, TcpDataType = TcpDataType.Compose)]
    public T Data { get; set; }
}
```
#### Built-in converters
Serializer use stock BitConverter and it support 10 types
```c#
typeof(bool)
typeof(char)
typeof(double)
typeof(short)
typeof(int)
typeof(long)
typeof(float)
typeof(ushort)
typeof(uint)
typeof(ulong)
```
#### Custom converters
For specific types you must create custom converter and pass it to TcpClientIoOptions

Converters below already included in package, but not added in list of converters when creating TcpClientIo
```c#
public class TcpDateTimeConverter : TcpConverter<DateTime>
{
    public override byte[] Convert(DateTime input) => BitConverter.GetBytes(input.ToBinary());
    public override DateTime ConvertBack(ReadOnlySpan<byte> input) => DateTime.FromBinary(BitConverter.ToInt64(input));
}

public class TcpGuidConverter : TcpConverter<Guid>
{
    public override byte[] Convert(Guid input) => input.ToByteArray();
    public override Guid ConvertBack(ReadOnlySpan<byte> input) => new Guid(input);
}

public class TcpUtf8StringConverter : TcpConverter<string>
{
    public override byte[] Convert(string input) => Encoding.UTF8.GetBytes(input);
    public override string ConvertBack(ReadOnlySpan<byte> input) => Encoding.UTF8.GetString(input);
}
```
```c#
var options = new TcpClientIoOptions
{
    Converters = new List<TcpConverter>
    {
        new TcpDateTimeConverter(),
        new TcpGuidConverter(),
        new TcpUtf8StringConverter()
    };
}

var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, options);
```
## Dependencies
* [AsyncEx](https://github.com/StephenCleary/AsyncEx)
* [WaitingDictionary](https://github.com/Drenalol/WaitingDictionary)
* [Microsoft.Extensions.Logging.Abstractions](https://github.com/dotnet/extensions#package-list)
* [System.IO.Pipelines](https://github.com/dotnet/runtime/tree/master/src/libraries/System.IO.Pipelines)
* [System.Threading.Tasks.Dataflow](https://github.com/dotnet/runtime/tree/master/src/libraries/System.Threading.Tasks.Dataflow)
* [System.Collections.Immutable](https://github.com/dotnet/runtime/tree/master/src/libraries/System.Collections.Immutable)
