using Drenalol.TcpClientIo.Batches;

namespace Drenalol.TcpClientIo.Contracts;

/// <summary>
/// Send/receive contract over TCP with schema serialization, keyed by <typeparamref name="TId"/>.
/// </summary>
public interface ITcpClientIo<in TId, in TRequest, TResponse> : ITcpClientIo
{
    /// <summary>
    /// Serializes and sends data asynchronously to a connected client.
    /// </summary>
    Task<bool> SendAsync(TRequest request, CancellationToken token = default);

    /// <summary>
    /// Begins an asynchronous request to receive the response associated with the specified ID.
    /// </summary>
    Task<ITcpBatch<TResponse>> ReceiveAsync(TId responseId, CancellationToken token = default);

    /// <summary>
    /// Provides a consuming <see cref="IAsyncEnumerable{T}"/> for <see cref="ITcpBatch{TResponse}"/> in the collection.
    /// </summary>
    IAsyncEnumerable<ITcpBatch<TResponse>> GetConsumingAsyncEnumerable(CancellationToken token = default);

    /// <summary>
    /// Provides a consuming <see cref="IAsyncEnumerable{T}"/> for every response in the collection, expanding batches.
    /// </summary>
    IAsyncEnumerable<TResponse> GetExpandableConsumingAsyncEnumerable(CancellationToken token = default);
}
