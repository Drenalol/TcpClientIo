using System.Collections;

namespace Drenalol.TcpClientIo.Batches;

/// <summary>
/// Default TcpBatch instance
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public sealed class DefaultTcpBatch<TResponse> : ITcpBatch<TResponse>
{
    private readonly List<TResponse> _internalList = [];

    /// <inheritdoc/>
    public int Count => _internalList.Count;

    /// <inheritdoc/>
    public void Add(TResponse response) => _internalList.Add(response);

    /// <inheritdoc/>
    public IEnumerator<TResponse> GetEnumerator() => _internalList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
