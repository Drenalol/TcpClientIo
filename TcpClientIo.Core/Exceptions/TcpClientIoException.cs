namespace Drenalol.TcpClientIo.Exceptions;

/// <summary>
/// Represents errors that occur during TcpClientIo transport execution.
/// </summary>
public class TcpClientIoException(string message) : Exception(message)
{
    /// <summary>
    /// Converter was registered without a generic <see cref="Converters.TcpConverter{T}"/> base.
    /// </summary>
    public static TcpClientIoException ConverterError(string converterName) => new($"Converter {converterName} does not have generic type");

    /// <summary>
    /// Singleton exception reused for every broken-connection signal to avoid capturing stack traces.
    /// </summary>
    public static readonly TcpClientIoException ConnectionBroken = new("Connection was broken");
}
