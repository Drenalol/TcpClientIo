namespace Drenalol.TcpClientIo.Converters;

/// <summary>
/// DateTime converter to byte array and vice versa.
/// </summary>
public class TcpDateTimeConverter : TcpConverter<DateTime>
{
    /// <inheritdoc/>
    public override byte[] Convert(DateTime input) => BitConverter.GetBytes(input.ToBinary());

    /// <inheritdoc/>
    public override DateTime ConvertBack(ReadOnlySpan<byte> input) => DateTime.FromBinary(BitConverter.ToInt64(input));
}
