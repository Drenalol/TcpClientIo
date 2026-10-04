namespace Drenalol.TcpClientIo.Converters;

/// <summary>
/// Non-generic converter contract used by the serializer.
/// </summary>
public abstract class TcpConverter
{
    /// <summary>
    /// Converts a value of the supported type to its byte representation.
    /// </summary>
    public abstract byte[] ConvertTo(object input);

    /// <summary>
    /// Converts bytes back to a value of the supported type.
    /// </summary>
    public abstract object ConvertBackTo(ReadOnlySpan<byte> input);
}

/// <summary>
/// Typed converter contract. Inherit this to plug custom type serialization into <see cref="Options.TcpClientIoOptions"/>.
/// </summary>
public abstract class TcpConverter<T> : TcpConverter
{
    /// <inheritdoc/>
    public sealed override byte[] ConvertTo(object input)
    {
        if (input is T genericInput)
            return Convert(genericInput);

        throw new ArgumentException($"Input must be of type {typeof(T)}", nameof(input));
    }

    /// <summary>
    /// Converts a value of type <typeparamref name="T"/> to its byte representation.
    /// </summary>
    public abstract byte[] Convert(T input);

    /// <inheritdoc/>
    /// <remarks>
    /// An empty span is a valid input (e.g. empty body) and is passed through to <see cref="ConvertBack"/>;
    /// converters with wrong span length throw on their own and are wrapped as TcpException by the serializer.
    /// </remarks>
    public sealed override object ConvertBackTo(ReadOnlySpan<byte> input) => ConvertBack(input)!;

    /// <summary>
    /// Converts bytes back to a value of type <typeparamref name="T"/>.
    /// </summary>
    public abstract T ConvertBack(ReadOnlySpan<byte> input);
}
