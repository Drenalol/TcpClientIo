namespace Drenalol.TcpClientIo.Converters;

/// <summary>
/// Guid converter to byte array and vice versa.
/// </summary>
public class TcpGuidConverter : TcpConverter<Guid>
{
    /// <inheritdoc/>
    public override byte[] Convert(Guid input) => input.ToByteArray();

    /// <inheritdoc/>
    public override Guid ConvertBack(ReadOnlySpan<byte> input) => new(input);
}
