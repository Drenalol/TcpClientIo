using System.Buffers;
using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff;

public sealed class MockMemoryBody : IEquatable<MockMemoryBody>
{
    [TcpData(0, 4, TcpDataType = TcpDataType.Id)]
    public int Id { get; set; }

    [TcpData(4, 4, TcpDataType = TcpDataType.Length)]
    public int Length { get; set; }

    [TcpData(8, 1)]
    public byte TestByte { get; set; }

    [TcpData(9, 2)]
    public byte[] TestByteArray { get; set; } = [];

    [TcpData(11, TcpDataType = TcpDataType.Body)]
    public ReadOnlySequence<byte> Body { get; set; }

    public bool Equals(MockMemoryBody? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Id == other.Id && Length == other.Length && TestByte == other.TestByte && TestByteArray.SequenceEqual(other.TestByteArray) && SequenceEquals(Body, other.Body);
    }

    public override bool Equals(object? obj) => Equals(obj as MockMemoryBody);

    public override int GetHashCode() => HashCode.Combine(Id, Length, TestByte, TestByteArray, Body.Length);

    public static bool operator ==(MockMemoryBody? left, MockMemoryBody? right) => Equals(left, right);

    public static bool operator !=(MockMemoryBody? left, MockMemoryBody? right) => !Equals(left, right);

    private static bool SequenceEquals(in ReadOnlySequence<byte> left, in ReadOnlySequence<byte> right) =>
        left.Length == right.Length && left.ToArray().AsSpan().SequenceEqual(right.ToArray());
}
