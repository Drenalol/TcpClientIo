using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Drenalol.Abstractions;
using Drenalol.Base;
using Drenalol.Exceptions;
using Drenalol.Extensions;
using Microsoft.Extensions.Logging;

namespace Drenalol.Helpers
{
    internal class BitConverterHelper
    {
        private readonly ImmutableDictionary<Type, MethodInfo> _builtInConvertersToBytes;
        private readonly ImmutableDictionary<Type, MethodInfo> _builtInConvertersFromBytes;
        private readonly ImmutableDictionary<Type, TcpConverter> _customConverters;
        private readonly ILogger _logger;

        public BitConverterHelper(ImmutableDictionary<Type, TcpConverter> converters, ILogger logger)
        {
            _customConverters = converters;
            _logger = logger;
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
                    throw TcpException.Throw(TcpTypeException.PropertyArgumentIsNull, _logger, propertyType.ToString());
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
                            throw TcpException.Throw(TcpTypeException.ConverterUnknownError, _logger, propertyType.ToString(), exception.Message);
                        }
                    }
                    else
                        throw TcpException.Throw(TcpTypeException.ConverterNotFoundType, _logger, propertyType.ToString());
            }
        }

        public object ConvertFromBytes(byte[] propertyValue, Type propertyType, bool reverse = false)
        {
            if (propertyValue == null)
                throw TcpException.Throw(TcpTypeException.PropertyArgumentIsNull, _logger, propertyType.ToString());

            if (propertyType == typeof(byte[]))
                return reverse ? Reverse(propertyValue) : propertyValue;

            if (propertyType == typeof(byte))
                return propertyValue.Length > 1 ? throw TcpException.Throw(TcpTypeException.ConverterUnknownError, _logger, propertyType.ToString(), "byte array > 1") : propertyValue[0];

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
                    throw TcpException.Throw(TcpTypeException.ConverterUnknownError, _logger, propertyType.ToString(), exception.Message);
                }
            }
            else
                throw TcpException.Throw(TcpTypeException.ConverterNotFoundType, _logger, propertyType.ToString());

            return result;
        }
    }
}