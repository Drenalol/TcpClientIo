using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization.Strategies;

/// <summary>
/// Serialized body, total message length and the converted value that the Length property must carry on the wire.
/// </summary>
internal record struct SerializeResult(ReadOnlySequence<byte>? Data, int Length, object? LengthValue);

internal abstract class SerializerStrategy<TData> where TData : notnull
{
    public abstract SerializeResult GetBodyData(TData value);
}
