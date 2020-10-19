using System;
using System.Collections.Immutable;

namespace Drenalol.TcpClientIo
{
    public static class TcpConverterExtension
    {
        public static bool TryConvert(this ImmutableDictionary<Type, TcpConverter> converters, Type type, object o, out byte[] result)
        {
            if (converters.TryGetValue(type, out var converter))
            {
                result = converter.ConvertTo(o);
                return true;
            }

            result = null;
            return false;
        }

        public static bool TryConvertBack(this ImmutableDictionary<Type, TcpConverter> converters, Type type, byte[] bytes, out object result)
        {
            if (converters.TryGetValue(type, out var converter))
            {
                result = converter.ConvertBackTo(bytes);
                return true;
            }

            result = default;
            return false;
        }
    }
}