using System.Buffers;

namespace Drenalol.TcpClientIo.Extensions;

/// <summary>
/// Helpers for <see cref="ReadOnlySequence{T}"/> and array pooling.
/// </summary>
public static class MemoryExtensions
{
    /// <summary>
    /// Wraps an array into a single-segment <see cref="ReadOnlySequence{T}"/> without copying.
    /// </summary>
    public static ReadOnlySequence<byte> ToSequence(this byte[] bytes) => new(bytes);

    /// <summary>
    /// Copies the sequence into a freshly allocated array and wraps it back into a single-segment sequence.
    /// </summary>
    public static ReadOnlySequence<byte> Clone(this in ReadOnlySequence<byte> sequence)
    {
        var sequenceLength = (int)sequence.Length;
        var bytes = GC.AllocateUninitializedArray<byte>(sequenceLength);

        sequence.CopyTo((Span<byte>)bytes);

        return new ReadOnlySequence<byte>(bytes, 0, sequenceLength);
    }
}
