using System;
using System.Linq;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpSerializer<TData>
    {
        private readonly Func<int, byte[]> _byteArrayFactory;
        private readonly ReflectionHelper _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public TcpSerializer(BitConverterHelper bitConverterHelper, Func<int, byte[]> byteArrayFactory)
        {
            _byteArrayFactory = byteArrayFactory;
            _bitConverterHelper = bitConverterHelper;
            _reflectionHelper = new ReflectionHelper(typeof(TData), byteArrayFactory, bitConverterHelper);
        }

        public SerializedRequest Serialize(TData data)
        {
            int realLength;
            var serializedBody = new byte[0];
            var examined = 0;
            SerializedRequest composeSerializedRequest = null;

            var properties = _reflectionHelper.Properties;

            if (_reflectionHelper.BodyProperty != null)
            {
                var bodyLengthProperty = properties.Single(p => p.Attribute.TcpDataType == TcpDataType.BodyLength);

                var bodyValue = _reflectionHelper.BodyProperty.Get(data);
                serializedBody = _bitConverterHelper.ConvertToBytes(bodyValue, _reflectionHelper.BodyProperty.PropertyType, _reflectionHelper.BodyProperty.Attribute.Reverse);

                if (serializedBody == null)
                    throw TcpException.SerializerBodyPropertyIsNull();

                var lengthValue = bodyLengthProperty.PropertyType == typeof(int)
                    ? serializedBody.Length
                    : Convert.ChangeType(serializedBody.Length, bodyLengthProperty.PropertyType);

                if (bodyLengthProperty.IsValueType)
                    data = (TData) bodyLengthProperty.Set(data, lengthValue);
                else
                    bodyLengthProperty.Set(data, lengthValue);

                try
                {
                    realLength = (int) lengthValue + _reflectionHelper.MetaLength;
                }
                catch (InvalidCastException)
                {
                    realLength = Convert.ToInt32(lengthValue) + _reflectionHelper.MetaLength;
                }
            }
            else if (_reflectionHelper.ComposeProperty != null)
            {
                var composeData = _reflectionHelper.ComposeProperty.Get(data);

                if (composeData == null)
                    throw TcpException.SerializerComposePropertyIsNull();

                composeSerializedRequest = _reflectionHelper.ComposeProperty.Composition.Serialize(composeData);
                realLength = composeSerializedRequest.RealLength + _reflectionHelper.MetaLength;
            }
            else
                realLength = _reflectionHelper.MetaLength;

            var rentedArray = _byteArrayFactory(realLength);

            foreach (var property in properties)
            {
                if (property.Attribute.TcpDataType == TcpDataType.Compose && composeSerializedRequest != null)
                    Array.Copy(
                        composeSerializedRequest.RentedArray,
                        0,
                        rentedArray,
                        property.Attribute.Index,
                        composeSerializedRequest.RealLength
                    );
                else
                {
                    var value = property.Attribute.TcpDataType == TcpDataType.Body
                        ? serializedBody
                        : _bitConverterHelper.ConvertToBytes(property.Get(data), property.PropertyType, property.Attribute.Reverse);

                    var valueLength = value.Length;

                    if (property.Attribute.TcpDataType != TcpDataType.Body && valueLength > property.Attribute.Length)
                        throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                    value.CopyTo(rentedArray, property.Attribute.Index);
                }

                if (++examined == properties.Count)
                    break;
            }

            return new SerializedRequest(rentedArray, realLength, composeSerializedRequest);
        }
    }
}