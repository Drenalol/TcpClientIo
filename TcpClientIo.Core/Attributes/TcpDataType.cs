namespace Drenalol.TcpClientIo.Attributes;

/// <summary>
/// Serialization rule for a property marked with <see cref="TcpDataAttribute"/>.
/// </summary>
public enum TcpDataType
{
    /// <summary>
    /// Regular header property, serialized as-is.
    /// </summary>
    MetaData,

    /// <summary>
    /// Identifier property, used to match responses with requests.
    /// </summary>
    Id,

    /// <summary>
    /// Body length property, overwritten by the serializer.
    /// </summary>
    Length,

    /// <summary>
    /// Body property, length is taken from the Length property.
    /// </summary>
    Body,

    /// <summary>
    /// Composed property.
    /// </summary>
    [Obsolete("Will be refactored in the future", true)]
    Compose
}
