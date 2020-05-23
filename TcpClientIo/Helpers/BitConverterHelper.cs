using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Drenalol.Base;
using Drenalol.Exceptions;
using Drenalol.Extensions;

namespace Drenalol.Helpers
{
    internal class BitConverterHelper
    {
        private readonly ImmutableDictionary<Type, MethodInfo> _builtInConvertersToBytes;
        private readonly ImmutableDictionary<Type, MethodInfo> _builtInConvertersFromBytes;
        private readonly ImmutableDictionary<Type, TcpPackageConverter> _customConverters;

        public BitConverterHelper(ImmutableDictionary<Type, TcpPackageConverter> converters)
        {
            _customConverters = converters;
            var toBytes = new Dictionary<Type, MethodInfo>();
            var fromBytes = new Dictionary<Type, MethodInfo>();
            
            Add(typeof(bool));
            Add(typeof(char));
            Add(typeof(double));
            Add(typeof(short));
            Add(typeof(int));
            Add(typeof(long));
            Add(typeof(float));
            Add(typeof(ushort));
            Add(typeof(uint));
            Add(typeof(ulong));

            _builtInConvertersToBytes = toBytes.ToImmutableDictionary();
            _builtInConvertersFromBytes = fromBytes.ToImmutableDictionary();

            void Add(Type type)
            {
                toBytes.Add(type, typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {type}));
                fromBytes.Add(type, typeof(BitConverter).GetMethod($"To{type.Name}", new[] {typeof(byte[]), typeof(int)}));
            }
        }

        public byte[] Reverse(byte[] bytes)
        {
            ((Span<byte>) bytes).Reverse();
            return bytes;
        }

        public byte[] ConvertToBytes(object propertyValue, Type propertyType, bool reverse = false)
        {
            switch (propertyValue)
            {
                case null:
                    throw TcpPackageException.Throw(TcpPackageTypeException.PropertyArgumentIsNull, propertyType.ToString());
                case byte[] byteArray:
                    return reverse ? Reverse(byteArray) : byteArray;
                default:
                    if (_customConverters.TryConvert(propertyType, propertyValue, out var result))
                        return reverse ? Reverse(result) : result;

                    if (_builtInConvertersToBytes.TryGetValue(propertyType, out var methodInfo))
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

        public object ConvertFromBytes(byte[] propertyValue, Type propertyType, bool reverse = false)
        {
            if (propertyValue == null)
                throw TcpPackageException.Throw(TcpPackageTypeException.PropertyArgumentIsNull, propertyType.ToString());

            if (_customConverters.TryConvertBack(propertyType, reverse ? Reverse(propertyValue) : propertyValue, out var result))
                return result;

            if (_builtInConvertersFromBytes.TryGetValue(propertyType, out var methodInfo))
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