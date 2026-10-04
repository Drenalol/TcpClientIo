using System.Buffers;
using System.Reflection;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Options;
using Nito.Disposables;

namespace Drenalol.TcpClientIo.Serialization;

internal class BitConverterHelper(TcpClientIoOptions options)
{
    private static readonly IReadOnlyDictionary<Type, Func<ReadOnlySpan<byte>, object>> BuiltInConvertersFromBytes = new Dictionary<Type, Func<ReadOnlySpan<byte>, object>>
    {
        [typeof(bool)] = static span => BitConverter.ToBoolean(span),
        [typeof(char)] = static span => BitConverter.ToChar(span),
        [typeof(double)] = static span => BitConverter.ToDouble(span),
        [typeof(short)] = static span => BitConverter.ToInt16(span),
        [typeof(int)] = static span => BitConverter.ToInt32(span),
        [typeof(long)] = static span => BitConverter.ToInt64(span),
        [typeof(float)] = static span => BitConverter.ToSingle(span),
        [typeof(ushort)] = static span => BitConverter.ToUInt16(span),
        [typeof(uint)] = static span => BitConverter.ToUInt32(span),
        [typeof(ulong)] = static span => BitConverter.ToUInt64(span)
    };

    private readonly IReadOnlyDictionary<Type, MethodInfo> _builtInConvertersToBytes = new Dictionary<Type, MethodInfo>
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

    private readonly IReadOnlyDictionary<Type, TcpConverter> _customConverters = options.Converters
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

    private static MethodInfo GetMethod(Type type) => typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), [type]) ?? throw new MissingMethodException();

    private static byte[] Reverse(byte[] bytes)
    {
        ((Span<byte>)bytes).Reverse();

        return bytes;
    }

    private static byte[] ReverseCopy(byte[] bytes)
    {
        var copy = new byte[bytes.Length];
        bytes.CopyTo(copy, 0);
        ((Span<byte>)copy).Reverse();

        return copy;
    }

    private static IDisposable MergeSpans(in ReadOnlySequence<byte> sequences, bool reverse, out ReadOnlySpan<byte> readOnlySpan)
    {
        if (!reverse && sequences.IsSingleSegment)
        {
            readOnlySpan = sequences.FirstSpan;

            return Disposable.Create(static () => { });
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
                return (reverse.GetValueOrDefault() ? ReverseCopy(byteArray) : byteArray).ToSequence();
            case ReadOnlySequence<byte> sequence:
                return sequence;
            default:
                try
                {
                    if (_customConverters.TryConvert(propertyType, propertyValue, out var result))
                        return (reverse.GetValueOrDefault() ? Reverse(result) : result).ToSequence();

                    if (!_builtInConvertersToBytes.TryGetValue(propertyType, out var methodInfo))
                        throw TcpException.ConverterNotFoundType(propertyType.ToString());

                    result = (byte[])methodInfo.Invoke(null, [propertyValue])!;
                    return (reverse ?? options.PrimitiveValueReverse ? Reverse(result) : result).ToSequence();
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
                ? reverse ?? options.PrimitiveValueReverse
                : reverse.GetValueOrDefault();

            using (MergeSpans(sequence, isPrimitiveReverse, out var span))
            {
                try
                {
                    if (_customConverters.TryConvertBack(propertyType, span, out var result))
                        return result;

                    if (BuiltInConvertersFromBytes.TryGetValue(propertyType, out var converter))
                        return converter(span);

                    throw TcpException.ConverterNotFoundType(propertyType.ToString());
                }
                catch (Exception exception) when (exception is not TcpException)
                {
                    throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                }
            }
        }
    }
}
