using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Options;
using Nito.Disposables;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class BitConverterHelper
    {
        private readonly IReadOnlyDictionary<Type, MethodInfo> _builtInConvertersToBytes;
        private readonly IReadOnlyDictionary<Type, TcpConverter> _customConverters;
        private readonly TcpClientIoOptions _options;

        public BitConverterHelper(TcpClientIoOptions options)
        {
            _options = options;
            _customConverters = options.Converters
                .Select(
                    converter =>
                    {
                        var converterType = converter.GetType();
                        var type = converterType.BaseType;

                        if (type == null)
                            throw TcpClientIoException.ConverterError(converterType.Name);

                        var genericType = type.GenericTypeArguments.Single();
                        return new KeyValuePair<Type, TcpConverter>(genericType, converter);
                    }
                )
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            _builtInConvertersToBytes = new Dictionary<Type, MethodInfo>
            {
                { typeof(bool), GetMethod(typeof(bool)) },
                { typeof(char), GetMethod(typeof(char)) },
                { typeof(double), GetMethod(typeof(double)) },
                { typeof(short), GetMethod(typeof(short)) },
                { typeof(int), GetMethod(typeof(int)) },
                { typeof(long), GetMethod(typeof(long)) },
                { typeof(float), GetMethod(typeof(float)) },
                { typeof(ushort), GetMethod(typeof(ushort)) },
                { typeof(uint), GetMethod(typeof(uint)) },
                { typeof(ulong), GetMethod(typeof(ulong)) }
            };

            MethodInfo GetMethod(Type type) => typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] { type }) ?? throw new MissingMethodException();
        }

        private static byte[] Reverse(byte[] bytes)
        {
            ((Span<byte>)bytes).Reverse();

            return bytes;
        }

        private static IDisposable MergeSpans(in ReadOnlySequence<byte> sequences, bool reverse, out ReadOnlySpan<byte> readOnlySpan)
        {
            if (!reverse && sequences.IsSingleSegment)
            {
                readOnlySpan = sequences.FirstSpan;

                return Disposable.Create(null);
            }

            var sequencesLength = (int)sequences.Length;
            var bytes = TcpSerializerBase.ArrayPool.Rent(sequencesLength);
            var span = new Span<byte>(bytes, 0, sequencesLength);
            sequences.CopyTo(span);

            if (reverse)
                span.Reverse();

            readOnlySpan = span;
            return Disposable.Create(() => TcpSerializerBase.ArrayPool.Return(bytes));
        }

        public ReadOnlySequence<byte> ConvertToSequence(object? propertyValue, Type propertyType, bool? reverse = null)
        {
            switch (propertyValue)
            {
                case null:
                    throw TcpException.PropertyArgumentIsNull(propertyType.ToString());
                case byte @byte:
                    return new[] { @byte }.ToSequence();
                case byte[] byteArray:
                    return (reverse.GetValueOrDefault() ? Reverse(byteArray) : byteArray).ToSequence();
                case ReadOnlySequence<byte> sequence:
                    return sequence;
                default:
                    try
                    {
                        if (_customConverters.TryConvert(propertyType, propertyValue, out var result))
                            return (reverse.GetValueOrDefault() ? Reverse(result) : result).ToSequence();

                        if (!_builtInConvertersToBytes.TryGetValue(propertyType, out var methodInfo))
                            throw TcpException.ConverterNotFoundType(propertyType.ToString());

                        result = (byte[])methodInfo.Invoke(null, new[] { propertyValue })!;
                        return (reverse ?? _options.PrimitiveValueReverse ? Reverse(result) : result).ToSequence();
                    }
                    catch (Exception exception) when (exception is not TcpException)
                    {
                        throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                    }
            }
        }

        public object ConvertFromSequence(in ReadOnlySequence<byte> slice, Type propertyType, bool? reverse = null)
        {
            if (propertyType == typeof(byte[]))
                return reverse.GetValueOrDefault() ? Reverse(slice.ToArray()) : slice.ToArray();

            if (propertyType == typeof(byte))
                return slice.FirstSpan[0];

            if (propertyType == typeof(ReadOnlySequence<byte>))
                return slice.Clone();

            return FromPrimitive(slice);

            object FromPrimitive(in ReadOnlySequence<byte> sequence)
            {
                var isPrimitiveReverse = propertyType.IsPrimitive
                    ? reverse ?? _options.PrimitiveValueReverse
                    : reverse.GetValueOrDefault();

                using (MergeSpans(sequence, isPrimitiveReverse, out var span))
                {
                    try
                    {
                        if (_customConverters.TryConvertBack(propertyType, span, out var result))
                            return result;

                        return propertyType.Name switch
                        {
                            nameof(Boolean) => BitConverter.ToBoolean(span),
                            nameof(Char) => BitConverter.ToChar(span),
                            nameof(Double) => BitConverter.ToDouble(span),
                            nameof(Int16) => BitConverter.ToInt16(span),
                            nameof(Int32) => BitConverter.ToInt32(span),
                            nameof(Int64) => BitConverter.ToInt64(span),
                            nameof(Single) => BitConverter.ToSingle(span),
                            nameof(UInt16) => BitConverter.ToUInt16(span),
                            nameof(UInt32) => BitConverter.ToUInt32(span),
                            nameof(UInt64) => BitConverter.ToUInt64(span),
                            _ => throw TcpException.ConverterNotFoundType(propertyType.ToString())
                        };
                    }
                    catch (Exception exception) when (exception is not TcpException)
                    {
                        throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                    }
                }
            }
        }
    }
}