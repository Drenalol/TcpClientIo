using System;
using System.Buffers;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Serialization.Strategies;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpSerializer<TData> : TcpSerializerBase where TData : notnull
    {
        private readonly Func<int, byte[]> _byteArrayFactory;
        private readonly ReflectionHelper _reflection;
        private readonly BitConverterHelper _bitConverter;
        private readonly SerializerStrategy<TData> _strategy;

        public TcpSerializer(BitConverterHelper bitConverterHelper, Func<int, byte[]> byteArrayFactory)
        {
            _byteArrayFactory = byteArrayFactory;
            _bitConverter = bitConverterHelper;
            _reflection = new ReflectionHelper(typeof(TData));
            _strategy = _reflection switch
            {
                { BodyProperty: not null, LengthProperty: not null } => new BodySerializerStrategy<TData>(_reflection, bitConverterHelper),
                _ => new EmptyBodySerializerStrategy<TData>(_reflection)
            };
        }

        public SerializedRequest Serialize(TData data)
        {
            var examined = 0;

            var (serializedBody, realLength) = _strategy.GetBodyData(data);

            var rentedArray = _byteArrayFactory(realLength);
            var memory = new Memory<byte>(rentedArray);

            foreach (var property in _reflection.Properties)
            {
                var value = property.Attribute.TcpDataType == TcpDataType.Body
                    ? serializedBody ?? throw TcpException.SerializerBodyPropertyIsNull()
                    : _bitConverter.ConvertToSequence(property.Get(data), property.PropertyType, property.Attribute.Reverse);

                var valueLength = value.Length;

                if (property.Attribute.TcpDataType != TcpDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                value.CopyTo(memory.Span[property.Attribute.Index..]);

                if (++examined == _reflection.Properties.Count)
                    break;
            }

            return new SerializedRequest(rentedArray, realLength);
        }
    }
}