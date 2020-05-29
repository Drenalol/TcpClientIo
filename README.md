# TcpClientIo
Wrapper of [TcpClient](https://github.com/dotnet/runtime/blob/c7a246c000747ec728ac862b7a503348b103df0e/src/libraries/System.Net.Sockets/src/System/Net/Sockets/TCPClient.cs "Source Code dotnet/corefx/TCPClient.cs") what help focus on **WHAT** you transfer over TCP not **HOW**

- Thread-safe
- Serialization with attribute schema
- Big/Little endian
- Async
- Cancellation support

[![NuGet Pre Release](https://img.shields.io/nuget/vpre/TcpClientIo.svg?style=for-the-badge&logo=appveyor)](https://www.nuget.org/packages/TcpClientIo/)
[![netstandard 2.0](https://img.shields.io/badge/netstandard-2.0-brightgreen.svg?style=for-the-badge&logo=appveyor)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![netstandard 2.1](https://img.shields.io/badge/netstandard-2.1-brightgreen.svg?style=for-the-badge&logo=appveyor)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) 
## Documentation
#### Prerequisites
Your TCP Server accepts and send messages with application-level header

##### Example byte array
| byte[] | 7B | 00 | 00 | 00 | 06 | 00 | 00 | 00 | 00 | D0 | 08 | A7 | 79 | 28 | B7 | 08 | A3 | 0B | 59 | 13 | 49 | 27 | 37 | 46 | B6 | D0 | 75 | A2 | EF | 07 | FA | 1F | 48 | 65 | 6C | 6C | 6F | 21 |
|--------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
##### Serialization process
| Property name | Index | Length | Bytes                                                            | Value                                          | Reverse | Change Type | Custom converter |
|---------------|--------|--------|------------------------------------------------------------------|------------------------------------------------|---------|-------------|------------------|
| Id            | 0      | 4      | [7B, 00, 00, 00]                                                 | 123                                            | false   | false       | false            |
| BodyLength    | 4      | 4      | [06, 00, 00, 00]                                                 | 6                                              | false   | false       | false            |
| DateTime      | 8      | 8      | [00, D0, 08, A7, 79, 28, B7, 08]                                 | "1991-02-07 10:00:00" as DateTime              | false   | false       | true             |
| Guid          | 16     | 16     | [A3, 0B, 59, 13, 49, 27, 37, 46, B6, D0, 75, A2, EF, 07, FA, 1F] | "13590ba3-2749-4637-b6d0-75a2ef07fa1f" as Guid | false   | false       | true             |
| Body          | 32     | 6      | [48, 65, 6C, 6C, 6F, 21]                                         | "Hello!" as string                             | false   | false       | true             |
#### Send & Receive
```c#
// Creating TcpClientIo instance with default options and different models of request/response
var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default);
// or when model used for both request/response
var tcpClient = new TcpClientIo<Request>(IPAddress.Any, 10000, TcpClientIoOptions.Default);

// Creating some request
Request request = new Request
{
    Id = 123U, // Serialized to [7B, 00, 00, 00], but if we set force reverse = true, it will serialized to [00, 00, 00, 7B] (My arch is Little-Endian)

    BodyLength = 6, // Serialized to [06, 00, 00, 00]

    // Custom converter DateTime to binary
    DateTime = DateTime.Parse("1991-02-07 10:00:00"), // Serialized to [00, D0, 08, A7, 79, 28, B7, 08]

    // Custom converter Guid to binary
    Guid = Guid.Parse("13590ba3-2749-4637-b6d0-75a2ef07fa1f"), // Serialized to [A3, 0B, 59, 13, 49, 27, 37, 46, B6, D0, 75, A2, EF, 07, FA, 1F]

    // Custom converter string to binary
    Data = "Hello!" // Serialized to [48, 65, 6C, 6C, 6F, 21]
};

// Send request asynchronously
await tcpClient.SendAsync(request, CancellationToken.None);

// Receive response in overtype ITcpBatch<Response> by identifier asynchronously
// !!! WARNING !!! Identifier is strongly-typed, if model set uint, you must pass it uint too
ITcpBatch<Response> resultBatch = await tcpClient.ReceiveAsync(123U, CancellationToken.None);

// Batch support iteration
foreach (var response in resultBatch)
{
    // code
}
// and LINQ queries
var response = resultBatch.First();

// Check result
Assert.AreEqual(request.Id, response.Id);
Assert.AreEqual(request.Size, response.Size);
Assert.AreEqual(request.Data, response.Data);

//Cleanup
await tcpClient.DisposeAsync();
```
#### Attribute schema
Request with first 32 bytes header, and body
```c#
public class Request
{
    // Attribute TcpData parameters:
    //
    // Index - Property position in Byte Array.
    // Length - Property length in Byte Array. If TcpDataType set to TcpDataType.Body, is ignored.
    // TcpDataType - Sets the serialization rule for this property.
    // Reverse - Reverses the sequence of the elements in the serialized Byte Array. Used for cases where the receiving side uses a different endianness.
    // Type - Sets the property type for the serializer.

    // Required
    [TcpData(0, 4, TcpDataType = TcpDataType.Id)]
    public uint Id { get; set; }

    // Required if TcpDataType.Body set
    [TcpData(4, 4, TcpDataType = TcpDataType.BodyLength)]
    public uint Size { get; set; }

    [TcpData(8, 8)]
    public DateTime DateTime { get; set; }

    [TcpData(16, 16)]
    public Guid Guid { get; set; }

    // Required if TcpDataType.BodyLength set
    [TcpData(32, TcpDataType = TcpDataType.Body)]
    public string Data { get; set; }
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
    public override DateTime ConvertBack(byte[] input) => DateTime.FromBinary(BitConverter.ToInt64(input));
}

public class TcpGuidConverter : TcpConverter<Guid>
{
    public override byte[] Convert(Guid input) => input.ToByteArray();
    public override Guid ConvertBack(byte[] input) => new Guid(input);
}

public class TcpUtf8StringConverter : TcpConverter<string>
{
    public override byte[] Convert(string input) => Encoding.UTF8.GetBytes(input);
    public override string ConvertBack(byte[] input) => Encoding.UTF8.GetString(input);
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
## TODO
 - [X] Add ILogger
 - [X] Code documentation
 - [X] netstandard2.0
## Dependencies
* [AsyncEx](https://github.com/StephenCleary/AsyncEx)
* [System.IO.Pipelines](https://github.com/dotnet/runtime/tree/master/src/libraries/System.IO.Pipelines)
* [System.Threading.Tasks.Dataflow](https://github.com/dotnet/runtime/tree/master/src/libraries/System.Threading.Tasks.Dataflow)