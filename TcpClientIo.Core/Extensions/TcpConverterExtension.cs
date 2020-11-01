using System;
using System.Collections.Generic;
using Drenalol.TcpClientIo.Converters;

namespace Drenalol.TcpClientIo.Extensions
{
    public static class TcpConverterExtension
    {
        public static bool TryConvert(this IReadOnlyDictionary<Type, TcpConverter> converters, Type type, object o, out byte[] result)
        {
            if (converters.TryGetValue(type, out var converter))
            {
                result = converter.ConvertTo(o);
                return true;
            }

            result = null;
            return false;
        }

        public static bool TryConvertBack(this IReadOnlyDictionary<Type, TcpConverter> converters, Type type, byte[] bytes, out object result)
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