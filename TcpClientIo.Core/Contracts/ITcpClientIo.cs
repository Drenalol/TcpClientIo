using System.IO.Pipelines;

namespace Drenalol.TcpClientIo.Contracts;

/// <summary>
/// Common non-generic surface of <see cref="Client.TcpClientIo{TInput,TOutput}"/>.
/// </summary>
public interface ITcpClientIo : IAsyncDisposable, IDuplexPipe
{
    /// <summary>
    /// Total bytes written to the NetworkStream.
    /// </summary>
    long BytesWrite { get; }

    /// <summary>
    /// Total bytes read from the NetworkStream.
    /// </summary>
    long BytesRead { get; }

    /// <summary>
    /// Number of responses to receive or the number of responses ready to receive.
    /// </summary>
    int Waiters { get; }

    /// <summary>
    /// Number of requests ready to send.
    /// </summary>
    int Requests { get; }

    /// <summary>
    /// Status of connection.
    /// </summary>
    bool IsBroken { get; }
}
