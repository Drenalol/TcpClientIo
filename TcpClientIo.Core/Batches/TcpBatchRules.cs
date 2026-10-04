namespace Drenalol.TcpClientIo.Batches;

/// <summary>
/// TcpBatch creation or update rules
/// </summary>
public class TcpBatchRules<TResponse>
{
    /// <summary>
    /// Creating rule of <see cref="ITcpBatch{TResponse}"/>
    /// </summary>
    public Func<TResponse, ITcpBatch<TResponse>> Create { get; set; } = null!;

    /// <summary>
    /// Update rule of <see cref="ITcpBatch{TResponse}"/>
    /// </summary>
    public Func<ITcpBatch<TResponse>, TResponse, ITcpBatch<TResponse>> Update { get; set; } = null!;

    /// <summary>
    /// Default rules for Create and Update
    /// </summary>
    public static TcpBatchRules<TResponse> Default => new()
    {
        Create = response => new DefaultTcpBatch<TResponse> { response },
        Update = (batch, response) =>
        {
            batch.Add(response);
            return batch;
        }
    };
}
