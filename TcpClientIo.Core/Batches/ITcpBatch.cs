namespace Drenalol.TcpClientIo.Batches;

/// <summary>
/// Batch of responses.
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public interface ITcpBatch<TResponse> : IEnumerable<TResponse>
{
    /// <summary>
    /// Number of responses in the batch.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Adds a response to the batch.
    /// </summary>
    void Add(TResponse response);
}
