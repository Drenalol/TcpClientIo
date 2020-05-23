using System;
using System.Collections.Immutable;
using Drenalol.Base;

namespace Drenalol.Extensions
{
    internal static class TcpPackageConverterExt
    {
        public static bool TryConvert(this ImmutableDictionary<Type, TcpPackageConverter> converters, Type type, object o, out byte[] result)
        {
            if (converters.TryGetValue(type, out var converter))
            {
                result = converter.ConvertTo(o);
                return true;
            }

            result = null;
            return false;
        }

        public static bool TryConvertBack(this ImmutableDictionary<Type, TcpPackageConverter> converters, Type type, byte[] bytes, out object result)
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