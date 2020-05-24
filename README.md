# TcpClientIo
Wrapper of [TcpClient](https://github.com/dotnet/runtime/blob/c7a246c000747ec728ac862b7a503348b103df0e/src/libraries/System.Net.Sockets/src/System/Net/Sockets/TCPClient.cs "Source Code dotnet/corefx/TCPClient.cs") what help focus on **WHAT** you transfer over TCP not **HOW**

- Thread-safe
- Big/Little endian
- Async
- Cancellation support

[![netstandard 2.0](https://img.shields.io/badge/netstandard-2.0-red.svg?style=for-the-badge&logo=appveyor)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![netstandard 2.1](https://img.shields.io/badge/netstandard-2.1-green.svg?style=for-the-badge&logo=appveyor)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) 
## Documentation
#### Prerequisites
Your TCP Server accepts and send messages with application-level header

##### Original
| Byte | 7B | 0 | 0 | 0 | 6 | 0 | 0 | 0 | 48 | 65 | 6C | 6C | 6F | 21 |
|------|----|---|---|---|---|---|---|---|----|----|----|----|----|----|
##### Serialized
| Offset         | 0  | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8  | 9  | 10 | 11 | 12 | 13 |
|---------------|----|---|---|---|---|---|---|---|----|----|----|----|----|----|
| ID            | 7B | 0 | 0 | 0 |   |   |   |   |    |    |    |    |    |    |
| LENGTH        |    |   |   |   | 6 | 0 | 0 | 0 |    |    |    |    |    |    |
| BODY          |    |   |   |   |   |   |   |   | 48 | 65 | 6C | 6C | 6F | 21 |
| Result        | 123 |  |   |   | 6 |   |   |   |  H |  e |  l |  l |  o |  ! |
#### Send & Receive
```c#
// Creating TcpClientIo instance with default options and different models of request/response
var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default);
// or when model used for both request/response
var tcpClient = new TcpClientIo<Request>(IPAddress.Any, 10000, TcpClientIoOptions.Default);

// Creating some request
Request request = new Request
{
    Id = 123U, // [7B, 0, 0, 0]
    Size = 6, // [6, 0, 0, 0]
    Data = "Hello!" // [48, 65, 6C, 6C, 6F, 21]
};

// Send request asynchronously
await tcpClient.SendAsync(request, CancellationToken.None);

// Receive response in overtype TcpPackageBatch<Response> by identifier asynchronously
// !!! WARNING !!! Identifier is strongly-typed, if model set uint, you must pass it uint too
TcpPackageBatch<Response> resultBatch = await tcpClient.ReceiveAsync(123U, CancellationToken.None);

// Batch support iteration
foreach (var response in resultBatch)
{
    // code
}
// OR LINQ queries
var response = resultBatch.First();

// Check result
Assert.AreEqual(request.Id, response.Id);
Assert.AreEqual(request.Size, response.Size);
Assert.AreEqual(request.Data, response.Data);
```
#### Attribute mapping
Request with first 32 bytes header, and body
```c#
public class Request
{
    // Attribute TcpPackageData parameters:
    //
    // Index (required)
    // Length (required if AttributeData != Body)
    // AttributeData = default MetaData (Id, Body, BodyLength, MetaData)
    // Reverse - force reverse byte array
    // Type - force set Type of property for Serializer

    // Required
    [TcpPackageData(0, 4, AttributeData = TcpPackageDataType.Id)]
    public uint Id { get; set; }

    // Required if TcpPackageDataType.Body set
    [TcpPackageData(4, 4, AttributeData = TcpPackageDataType.BodyLength)]
    public uint Size { get; set; }

    [TcpPackageData(8, 8)]
    public DateTime DateTime { get; set; }

    [TcpPackageData(16, 16)]
    public Guid Guid { get; set; }

    // Required if TcpPackageDataType.Body set
    [TcpPackageData(32, AttributeData = TcpPackageDataType.Body)]
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
For specific types you need create custom converter and pass it to TcpClientIoOptions

Converters below already included in package, but not added in list of converters when creating TcpClientIo
```c#
public class TcpPackageDateTimeConverter : TcpPackageConverter<DateTime>
{
    public override byte[] Convert(DateTime input) => BitConverter.GetBytes(input.ToBinary());
    public override DateTime ConvertBack(byte[] input) => DateTime.FromBinary(BitConverter.ToInt64(input));
}

public class TcpPackageGuidConverter : TcpPackageConverter<Guid>
{
    public override byte[] Convert(Guid input) => input.ToByteArray();
    public override Guid ConvertBack(byte[] input) => new Guid(input);
}

public class TcpPackageUtf8StringConverter : TcpPackageConverter<string>
{
    public override byte[] Convert(string input) => Encoding.UTF8.GetBytes(input);
    public override string ConvertBack(byte[] input) => Encoding.UTF8.GetString(input);
}
```
```c#
var options = new TcpClientIoOptions
{
    Converters = new List<TcpPackageConverter>
    {
        new TcpPackageDateTimeConverter()
    };
}

var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, options);
```
## TODO
 - Add ILogger
 - Code documentation
 - netstandard2.0?
## Dependencies
* [AsyncEx](https://github.com/StephenCleary/AsyncEx)
* [System.IO.Pipelines](https://github.com/dotnet/runtime/tree/master/src/libraries/System.IO.Pipelines)
* [System.Threading.Tasks.Dataflow](https://github.com/dotnet/runtime/tree/master/src/libraries/System.Threading.Tasks.Dataflow)