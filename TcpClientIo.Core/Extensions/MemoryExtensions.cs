using System;
using System.Buffers;

namespace Drenalol.TcpClientIo.Extensions;

public static class MemoryExtensions
{
    public static ReadOnlySequence<byte> ToSequence(this byte[] bytes) => new(bytes);

    public static ReadOnlySequence<byte> Clone(this in ReadOnlySequence<byte> sequence)
    {
        var sequenceLength = (int)sequence.Length;
        var bytes = GC.AllocateUninitializedArray<byte>(sequenceLength);

        sequence.CopyTo((Span<byte>)bytes);

        return new ReadOnlySequence<byte>(bytes, 0, sequenceLength);
    }
}