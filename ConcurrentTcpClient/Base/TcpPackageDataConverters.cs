using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Drenalol.Converters;

namespace Drenalol.Base
{
    public static class TcpPackageDataConverters
    {
        private static ImmutableDictionary<Type, ITcpPackageConverter> _converters = ImmutableDictionary<Type, ITcpPackageConverter>.Empty;

        static TcpPackageDataConverters()
        {
            Register<string, TcpPackageStringConverter>();
            Register<DateTime, TcpPackageDateTimeConverter>();
            Register<Guid, TcpPackageGuidConverter>();
        }

        public static bool TryConvert(Type type, object o, out byte[] result)
        {
            if (_converters.TryGetValue(type, out var converter))
            {
                result = converter.Convert(o);
                return true;
            }

            result = null;
            return false;
        }

        public static bool TryConvertBack(Type type, byte[] bytes, out object result)
        {
            if (_converters.TryGetValue(type, out var converter))
            {
                result = converter.ConvertBack(bytes);
                return true;
            }

            result = default;
            return false;
        }

        public static void Register<TInOut, TConverter>() where TConverter : ITcpPackageConverter, new()
        {
            var type = typeof(TInOut);
            var converter = new TConverter();
            
            if (!ImmutableInterlocked.TryAdd(ref _converters, type, converter))
                throw new ArgumentException($"From {type} converter already exists");
            
            Debug.WriteLine($"Converter added {type} -> {converter.ToString()}");
        }
    }
}