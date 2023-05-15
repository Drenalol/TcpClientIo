using System;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization.Strategies
{
    internal class BodySerializerStrategy<TData> : SerializerStrategy<TData> where TData : notnull
    {
        private readonly ReflectionHelper _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public BodySerializerStrategy(ReflectionHelper reflectionHelper, BitConverterHelper bitConverterHelper)
        {
            _reflectionHelper = reflectionHelper;
            _bitConverterHelper = bitConverterHelper;
        }

        public override SerailizeResult GetBodyData(TData value)
        {
            var bodyValue = _reflectionHelper.BodyProperty!.Get(value);

            if (bodyValue == null)
                throw TcpException.SerializerBodyPropertyIsNull();

            var serializedBody = _bitConverterHelper.ConvertToSequence(bodyValue, _reflectionHelper.BodyProperty.PropertyType, _reflectionHelper.BodyProperty.Attribute.Reverse);

            var realLength = CalculateRealLength(_reflectionHelper.LengthProperty!, ref value, _reflectionHelper.MetaLength, (int)serializedBody.Length);

            return new SerailizeResult(serializedBody, realLength);
        }

        private static int CalculateRealLength(
            TcpProperty lengthProperty,
            ref TData data,
            int metaLength,
            int dataLength
        )
        {
            var lengthValue = lengthProperty.PropertyType == typeof(int)
                ? dataLength
                : Convert.ChangeType(dataLength, lengthProperty.PropertyType);

            if (lengthProperty.IsValueType)
                data = (TData)lengthProperty.Set(data, lengthValue);
            else
                lengthProperty.Set(data, lengthValue);

            try
            {
                return (int)lengthValue + metaLength;
            }
            catch (InvalidCastException)
            {
                return Convert.ToInt32(lengthValue) + metaLength;
            }
        }
    }
}