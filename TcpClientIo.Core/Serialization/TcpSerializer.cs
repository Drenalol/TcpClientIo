using System;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpSerializer<TData>
    {
        private readonly Func<int, byte[]> _byteArrayFactory;
        private readonly ReflectionHelper _reflection;
        private readonly BitConverterHelper _bitConverter;

        public TcpSerializer(BitConverterHelper bitConverterHelper, Func<int, byte[]> byteArrayFactory)
        {
            _byteArrayFactory = byteArrayFactory;
            _bitConverter = bitConverterHelper;
            _reflection = new ReflectionHelper(typeof(TData), byteArrayFactory, bitConverterHelper);
        }

        public SerializedRequest Serialize(TData data)
        {
            int realLength;
            byte[] serializedBody = null;
            SerializedRequest composeSerializedRequest = null;
            var examined = 0;

            if (_reflection.BodyProperty != null)
            {
                var bodyValue = _reflection.BodyProperty.Get(data);
                
                if (bodyValue == null)
                    throw TcpException.SerializerBodyPropertyIsNull();
                
                serializedBody = _bitConverter.ConvertToBytes(bodyValue, _reflection.BodyProperty.PropertyType, _reflection.BodyProperty.Attribute.Reverse);
                realLength = CalculateRealLength(_reflection.LengthProperty, ref data, _reflection.MetaLength, serializedBody.Length);
            }
            else if (_reflection.ComposeProperty != null)
            {
                var composeValue = _reflection.ComposeProperty.Get(data);

                if (composeValue == null)
                    throw TcpException.SerializerComposePropertyIsNull();

                if (_reflection.ComposeProperty.PropertyType.IsPrimitive)
                {
                    serializedBody = _bitConverter.ConvertToBytes(composeValue, _reflection.ComposeProperty.PropertyType, _reflection.ComposeProperty.Attribute.Reverse);
                    realLength = CalculateRealLength(_reflection.LengthProperty, ref data, _reflection.MetaLength, serializedBody.Length);
                }
                else
                {
                    composeSerializedRequest = _reflection.ComposeProperty.Composition.Serialize(composeValue);
                    realLength = CalculateRealLength(_reflection.LengthProperty, ref data, _reflection.MetaLength, composeSerializedRequest.RealLength);   
                }
            }
            else
                realLength = _reflection.MetaLength;

            var rentedArray = _byteArrayFactory(realLength);

            foreach (var property in _reflection.Properties)
            {
                if (!property.PropertyType.IsPrimitive && property.Attribute.TcpDataType == TcpDataType.Compose && composeSerializedRequest != null)
                    Array.Copy(
                        composeSerializedRequest.RentedArray,
                        0,
                        rentedArray,
                        property.Attribute.Index,
                        composeSerializedRequest.RealLength
                    );
                else
                {
                    var value = property.Attribute.TcpDataType == TcpDataType.Body || property.Attribute.TcpDataType == TcpDataType.Compose
                        ? serializedBody ?? throw TcpException.SerializerBodyPropertyIsNull()
                        : _bitConverter.ConvertToBytes(property.Get(data), property.PropertyType, property.Attribute.Reverse);

                    var valueLength = value.Length;

                    if (property.Attribute.TcpDataType != TcpDataType.Body && property.Attribute.TcpDataType != TcpDataType.Compose && valueLength > property.Attribute.Length)
                        throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                    value.CopyTo(rentedArray, property.Attribute.Index);
                }

                if (++examined == _reflection.Properties.Count)
                    break;
            }

            return new SerializedRequest(rentedArray, realLength, composeSerializedRequest);

            static int CalculateRealLength(TcpProperty lengthProperty, ref TData data, int metaLength, object dataLength)
            {
                var lengthValue = lengthProperty.PropertyType == typeof(int)
                    ? dataLength
                    : Convert.ChangeType(dataLength, lengthProperty.PropertyType);

                if (lengthProperty.IsValueType)
                    data = (TData) lengthProperty.Set(data, lengthValue);
                else
                    lengthProperty.Set(data, lengthValue);

                try
                {
                    return (int) lengthValue + metaLength;
                }
                catch (InvalidCastException)
                {
                    return Convert.ToInt32(lengthValue) + metaLength;
                }
            }
        }
    }
}