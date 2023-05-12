using System.Buffers;

namespace Drenalol.TcpClientIo.Extensions;

public static class MemoryExtensions
{
    public static ReadOnlySequence<byte> ToSequence(this byte[] bytes) => new(bytes);

    /// <summary>
    /// Clone sequence by linked memory segments
    /// </summary>
    /// <param name="sequence"></param>
    /// <returns></returns>
    public static ReadOnlySequence<byte> Clone(this in ReadOnlySequence<byte> sequence)
    {
        var start = sequence.Start;
        MemorySegment<byte>? firstSegment = null;
        MemorySegment<byte>? lastSegment = null;

        while (sequence.TryGet(ref start, out var memory))
        {
            if (firstSegment == null)
                firstSegment = new MemorySegment<byte>(memory);
            else
                lastSegment = (lastSegment ?? firstSegment).Append(memory);
        }

        if (firstSegment == null)
            return new ReadOnlySequence<byte>();

        lastSegment ??= firstSegment; // if sequence IsSingleSegment == true

        return new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
    }
}