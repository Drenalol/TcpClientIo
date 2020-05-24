## What is it
TcpClientIo is a wrapper of [TcpClient](https://github.com/dotnet/runtime/blob/c7a246c000747ec728ac862b7a503348b103df0e/src/libraries/System.Net.Sockets/src/System/Net/Sockets/TCPClient.cs "Source Code dotnet/corefx/TCPClient.cs") what help focus on **WHAT** you transfer over TCP not **HOW**
## Documentation
#### Prerequisites
Your TCP Server accepts and send messages with application-level header

Example (byte-order and offsets not necessary)

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
// Creating TcpClientIo instance with default options
var tcpClient = new TcpClientIo<Request, Response>(IPAddress.Any, 10000, TcpClientIoOptions.Default));

// Creating some request
Request request = new Request
{
    Id = 123, // [7B, 0, 0, 0]
    Size = 6, // [6, 0, 0, 0]
    Data = "Hello!" // [48, 65, 6C, 6C, 6F, 21]
}

// Send request asynchronously
await tcpClient.SendAsync(request, CancellationToken.None);

// Receive response with specific identifier asynchronously
TcpPackageBatch<Response> resultBatch = await tcpClient.ReceiveAsync(123, CancellationToken.None);

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
#### Attribute Mapping
```c#

```
#### Custom Converters
```c#

```
## TODO
 - Implement ILogger interface
## Dependencies
* [AsyncEx](https://github.com/StephenCleary/AsyncEx)