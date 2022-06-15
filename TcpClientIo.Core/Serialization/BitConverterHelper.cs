using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Options;

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
            _customConverters = options.Converters.Select(
                converter =>
                {
                    var converterType = converter.GetType();
                    var type = converterType.BaseType;

                    if (type == null)
                        throw TcpClientIoException.ConverterError(converterType.Name);

                    var genericType = type.GenericTypeArguments.Single();
                    return new KeyValuePair<Type, TcpConverter>(genericType, converter);
                }
            ).ToDictionary(pair => pair.Key, pair => pair.Value);

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

        private static Sequence MergeSpans(ReadOnlySequence<byte> sequences, bool reverse)
        {
            if (!reverse && sequences.IsSingleSegment)
                return Sequence.Create(sequences.FirstSpan, null);

            var sequencesLength = (int)sequences.Length;
            var bytes = ArrayPool<byte>.Shared.Rent(sequencesLength);
            var span = new Span<byte>(bytes, 0, sequencesLength);
            sequences.CopyTo(span);

            if (reverse)
                span.Reverse();

            return Sequence.Create(span, () => ArrayPool<byte>.Shared.Return(bytes));
        }

        public byte[] ConvertToBytes(object? propertyValue, Type propertyType, bool? reverse = null)
        {
            switch (propertyValue)
            {
                case null:
                    throw TcpException.PropertyArgumentIsNull(propertyType.ToString());
                case byte @byte:
                    return new[] { @byte };
                case byte[] byteArray:
                    return reverse.GetValueOrDefault() ? Reverse(byteArray) : byteArray;
                default:
                    try
                    {
                        if (_customConverters.TryConvert(propertyType, propertyValue, out var result))
                            return reverse.GetValueOrDefault() ? Reverse(result) : result;

                        if (!_builtInConvertersToBytes.TryGetValue(propertyType, out var methodInfo))
                            throw TcpException.ConverterNotFoundType(propertyType.ToString());

                        result = (byte[])methodInfo.Invoke(null, new[] { propertyValue })!;
                        return reverse ?? _options.PrimitiveValueReverse ? Reverse(result) : result;
                    }
                    catch (Exception exception) when (!(exception is TcpException))
                    {
                        throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                    }
            }
        }

        public object ConvertFromBytes(ReadOnlySequence<byte> slice, Type propertyType, bool? reverse = null)
        {
            if (propertyType == typeof(byte[]))
                return reverse.GetValueOrDefault() ? Reverse(slice.ToArray()) : slice.ToArray();

            if (propertyType == typeof(byte))
                return slice.FirstSpan[0];

            var (span, returnArray) = MergeSpans(slice, propertyType.IsPrimitive ? reverse ?? _options.PrimitiveValueReverse : reverse.GetValueOrDefault());

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
            catch (Exception exception) when (!(exception is TcpException))
            {
                throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
            }
            finally
            {
                returnArray?.Invoke();
            }
        }
    }
}