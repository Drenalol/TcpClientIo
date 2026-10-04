using Drenalol.TcpClientIo.Converters;

namespace Drenalol.TcpClientIo.Extensions;

/// <summary>
/// Dictionary lookup helpers for <see cref="TcpConverter"/> registries.
/// </summary>
public static class TcpConverterExtension
{
    /// <summary>
    /// Converts the value when a converter for <paramref name="type"/> is registered.
    /// </summary>
    public static bool TryConvert(this IReadOnlyDictionary<Type, TcpConverter> converters, Type type, object o, out byte[] result)
    {
        if (converters.TryGetValue(type, out var converter))
        {
            result = converter.ConvertTo(o);
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// Converts bytes back when a converter for <paramref name="type"/> is registered.
    /// </summary>
    public static bool TryConvertBack(this IReadOnlyDictionary<Type, TcpConverter> converters, Type type, ReadOnlySpan<byte> span, out object result)
    {
        if (converters.TryGetValue(type, out var converter))
        {
            result = converter.ConvertBackTo(span);
            return true;
        }

        result = default!;
        return false;
    }
}
