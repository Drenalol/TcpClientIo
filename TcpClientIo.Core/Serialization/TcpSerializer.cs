using System;
using System.Collections.Generic;
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
            var realLength = 0;
            var serializedBody = new byte[0];
            var examined = 0;
            List<byte[]> composeRentedArrays = null;

            var properties = _reflectionHelper.Properties;
            var bodyProperty = properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Body);

            if (bodyProperty != null)
            {
                var bodyLengthProperty = properties.Single(p => p.Attribute.TcpDataType == TcpDataType.BodyLength);

                var bodyValue = bodyProperty.Get(data);
                serializedBody = _bitConverterHelper.ConvertToBytes(bodyValue, bodyProperty.PropertyType, bodyProperty.Attribute.Reverse);

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
                    realLength += (int) lengthValue + _reflectionHelper.MetaLength;
                }
                catch (InvalidCastException)
                {
                    realLength += Convert.ToInt32(lengthValue) + _reflectionHelper.MetaLength;
                }
            }
            else
                realLength += _reflectionHelper.MetaLength;

            byte[] rentedArray = null;

            var composeProperties = properties.Where(p => p.IsCompose).ToArray();

            foreach (var (composePropertyIndex, composeSerializedRequest) in composeProperties.Select(composeProperty =>
            {
                var composeData = composeProperty.Get(data);

                if (composeData == null)
                    throw TcpException.SerializerComposePropertyIsNull();
                
                var composeSerializedRequest = composeProperty.Composition.Serialize(composeData);
                (composeRentedArrays ??= new List<byte[]>()).Add(composeSerializedRequest.RentedArray);
                realLength += composeSerializedRequest.RealLength;
                return (composeProperty.Attribute.Index, composeSerializedRequest);
            }))
            {
                rentedArray ??= _byteArrayFactory(realLength);
                Array.Copy(composeSerializedRequest.RentedArray, 0, rentedArray, composePropertyIndex, composeSerializedRequest.RealLength);
            }

            foreach (var property in properties.Where(p => !p.IsCompose))
            {
                rentedArray ??= _byteArrayFactory(realLength);
                
                var value = property.Attribute.TcpDataType == TcpDataType.Body
                    ? serializedBody
                    : _bitConverterHelper.ConvertToBytes(property.Get(data), property.PropertyType, property.Attribute.Reverse);

                var valueLength = value.Length;

                if (property.Attribute.TcpDataType != TcpDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                value.CopyTo(rentedArray, property.Attribute.Index);

                if (++examined == properties.Count)
                    break;
            }

            return new SerializedRequest(rentedArray, realLength, composeRentedArrays);
        }
    }
}