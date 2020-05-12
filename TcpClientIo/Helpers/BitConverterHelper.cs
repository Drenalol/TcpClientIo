using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Drenalol.Base;
using Drenalol.Exceptions;

namespace Drenalol.Helpers
{
    public static class BitConverterHelper
    {
        private static readonly ImmutableDictionary<Type, MethodInfo> DictionaryToBytes;
        private static readonly ImmutableDictionary<Type, MethodInfo> DictionaryFromBytes;

        static BitConverterHelper()
        {
            var toBytes = new Dictionary<Type, MethodInfo>();
            AddTo(toBytes, typeof(bool));
            AddTo(toBytes, typeof(char));
            AddTo(toBytes, typeof(double));
            AddTo(toBytes, typeof(short));
            AddTo(toBytes, typeof(int));
            AddTo(toBytes, typeof(long));
            AddTo(toBytes, typeof(float));
            AddTo(toBytes, typeof(ushort));
            AddTo(toBytes, typeof(uint));
            AddTo(toBytes, typeof(ulong));

            var fromBytes = new Dictionary<Type, MethodInfo>();
            AddFrom(fromBytes, typeof(char));
            AddFrom(fromBytes, typeof(short));
            AddFrom(fromBytes, typeof(int));
            AddFrom(fromBytes, typeof(long));
            AddFrom(fromBytes, typeof(ushort));
            AddFrom(fromBytes, typeof(uint));
            AddFrom(fromBytes, typeof(ulong));
            AddFrom(fromBytes, typeof(float));
            AddFrom(fromBytes, typeof(double));
            AddFrom(fromBytes, typeof(string));
            AddFrom(fromBytes, typeof(bool));

            DictionaryToBytes = toBytes.ToImmutableDictionary();
            DictionaryFromBytes = fromBytes.ToImmutableDictionary();

            static void AddTo(IDictionary<Type, MethodInfo> dict, Type type) => dict.Add(type, typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {type}));
            static void AddFrom(IDictionary<Type, MethodInfo> dict, Type type) => dict.Add(type, typeof(BitConverter).GetMethod($"To{type.Name}", new[] {typeof(byte[]), typeof(int)}));
        }

        internal static byte[] Reverse(byte[] bytes)
        {
            new Span<byte>(bytes).Reverse();
            return bytes;
        }

        public static byte[] CustomBitConverterToBytes(object propertyValue, Type propertyType, bool reverse = false)
        {
            switch (propertyValue)
            {
                case null:
                    throw TcpPackageException.Throw(TcpPackageTypeException.PropertyArgumentIsNull, propertyType.ToString());
                case byte[] byteArray:
                    return reverse ? Reverse(byteArray) : byteArray;
                default:
                    if (TcpPackageDataConverters.TryConvert(propertyType, propertyValue, out var result))
                        return reverse ? Reverse(result) : result;

                    if (DictionaryToBytes.TryGetValue(propertyType, out var methodInfo))
                    {
                        try
                        {
                            result = (byte[]) methodInfo.Invoke(null, new[] {propertyValue});
                        }
                        catch (Exception exception)
                        {
                            throw TcpPackageException.Throw(TcpPackageTypeException.ConverterUnknownError, propertyType.ToString(), exception.Message);
                        }
                    }
                    else
                        throw TcpPackageException.Throw(TcpPackageTypeException.ConverterNotFoundType, propertyType.ToString());

                    return reverse ? Reverse(result) : result;
            }
        }

        public static object CustomBitConverterFromBytes(byte[] propertyValue, Type propertyType, bool reverse = false)
        {
            if (propertyValue == null)
                throw TcpPackageException.Throw(TcpPackageTypeException.PropertyArgumentIsNull, propertyType.ToString());

            if (TcpPackageDataConverters.TryConvertBack(propertyType, reverse ? Reverse(propertyValue) : propertyValue, out var result))
                return result;

            if (DictionaryFromBytes.TryGetValue(propertyType, out var methodInfo))
            {
                try
                {
                    result = methodInfo.Invoke(null, new object[] {reverse ? Reverse(propertyValue) : propertyValue, 0});
                }
                catch (Exception exception)
                {
                    throw TcpPackageException.Throw(TcpPackageTypeException.ConverterUnknownError, propertyType.ToString(), exception.Message);
                }
            }
            else
                throw TcpPackageException.Throw(TcpPackageTypeException.ConverterNotFoundType, propertyType.ToString());

            return result;
        }
    }
}