using System.Buffers;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Serialization.Strategies;

namespace Drenalol.TcpClientIo.Serialization;

internal class TcpSerializer<TData> : TcpSerializerBase where TData : notnull
{
    private readonly BitConverterHelper _bitConverterHelper;
    private readonly Func<int, byte[]> _byteArrayFactory;
    private readonly ReflectionHelper _reflection;
    private readonly SerializerStrategy<TData> _strategy;

    public TcpSerializer(BitConverterHelper bitConverterHelper, Func<int, byte[]> byteArrayFactory)
    {
        _bitConverterHelper = bitConverterHelper;
        _byteArrayFactory = byteArrayFactory;
        _reflection = new ReflectionHelper(typeof(TData));
        _strategy = _reflection switch
        {
            { BodyProperty: not null, LengthProperty: not null } => new BodySerializerStrategy<TData>(_reflection, bitConverterHelper),
            _ => new EmptyBodySerializerStrategy<TData>(_reflection)
        };
    }

    public SerializedRequest Serialize(TData data)
    {
        var (serializedBody, realLength, lengthValue) = _strategy.GetBodyData(data);

        var rentedArray = _byteArrayFactory(realLength);
        var memory = new Memory<byte>(rentedArray);

        foreach (var property in _reflection.Properties)
        {
            // the Length property must carry the real body length on the wire even for value-type schemas,
            // where mutating the boxed copy inside the strategy does not reach this local copy
            var value = property.Attribute.TcpDataType switch
            {
                TcpDataType.Body => serializedBody ?? throw TcpException.SerializerBodyPropertyIsNull(),
                TcpDataType.Length when lengthValue is not null => _bitConverterHelper.ConvertToSequence(lengthValue, property.PropertyType, property.Attribute.Reverse),
                _ => _bitConverterHelper.ConvertToSequence(property.Get(data), property.PropertyType, property.Attribute.Reverse)
            };

            var valueLength = value.Length;

            if (property.Attribute.TcpDataType != TcpDataType.Body)
            {
                if (valueLength > property.Attribute.Length)
                    throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                // zero-pad the fixed-size slot: a rented array may contain bytes from previous frames
                memory.Span[property.Attribute.Index..(property.Attribute.Index + property.Attribute.Length)].Clear();
            }

            value.CopyTo(memory.Span[property.Attribute.Index..]);
        }

        return new SerializedRequest(rentedArray, realLength);
    }
}
