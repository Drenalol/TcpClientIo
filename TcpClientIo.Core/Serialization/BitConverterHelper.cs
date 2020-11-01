using System;
using System.Collections.Generic;
using System.Reflection;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;

namespace Drenalol.TcpClientIo.Serialization
{
    public class BitConverterHelper
    {
        private readonly IReadOnlyDictionary<Type, MethodInfo> _builtInConvertersToBytes;
        private readonly IReadOnlyDictionary<Type, MethodInfo> _builtInConvertersFromBytes;
        private readonly IReadOnlyDictionary<Type, TcpConverter> _customConverters;

        public BitConverterHelper(IReadOnlyDictionary<Type, TcpConverter> converters)
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

            _builtInConvertersToBytes = toBytes;
            _builtInConvertersFromBytes = fromBytes;

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
                    throw TcpException.PropertyArgumentIsNull(propertyType.ToString());
                case byte @byte:
                    return new[] {@byte};
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

                            return reverse ? Reverse(result) : result;
                        }
                        catch (Exception exception)
                        {
                            throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                        }
                    }
                    else
                        throw TcpException.ConverterNotFoundType(propertyType.ToString());
            }
        }

        public object ConvertFromBytes(byte[] propertyValue, Type propertyType, bool reverse = false)
        {
            if (propertyValue == null)
                throw TcpException.PropertyArgumentIsNull(propertyType.ToString());

            if (propertyType == typeof(byte[]))
                return reverse ? Reverse(propertyValue) : propertyValue;

            if (propertyType == typeof(byte))
                return propertyValue.Length > 1
                    ? throw TcpException.ConverterUnknownError(propertyType.ToString(), "value Length more than one byte")
                    : propertyValue[0];

            var bytes = reverse ? Reverse(propertyValue) : propertyValue;

            if (_customConverters.TryConvertBack(propertyType, bytes, out var result))
                return result;

            if (_builtInConvertersFromBytes.TryGetValue(propertyType, out var methodInfo))
            {
                try
                {
                    result = methodInfo.Invoke(null, new object[] {bytes, 0});
                }
                catch (Exception exception)
                {
                    throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                }
            }
            else
                throw TcpException.ConverterNotFoundType(propertyType.ToString());

            return result;
        }
    }
}