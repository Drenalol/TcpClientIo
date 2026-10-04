using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization.Strategies;

internal class BodySerializerStrategy<TData>(ReflectionHelper reflectionHelper, BitConverterHelper bitConverterHelper) : SerializerStrategy<TData> where TData : notnull
{
    public override SerializeResult GetBodyData(TData value)
    {
        var bodyValue = reflectionHelper.BodyProperty!.Get(value);

        if (bodyValue == null)
            throw TcpException.SerializerBodyPropertyIsNull();

        var serializedBody = bitConverterHelper.ConvertToSequence(bodyValue, reflectionHelper.BodyProperty.PropertyType, reflectionHelper.BodyProperty.Attribute.Reverse);

        var (realLength, lengthValue) = CalculateRealLength(reflectionHelper.LengthProperty!, ref value, reflectionHelper.MetaLength, (int)serializedBody.Length);

        return new SerializeResult(serializedBody, realLength, lengthValue);
    }

    private static (int Length, object LengthValue) CalculateRealLength(
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
            return ((int)lengthValue + metaLength, lengthValue);
        }
        catch (InvalidCastException)
        {
            return (Convert.ToInt32(lengthValue) + metaLength, lengthValue);
        }
    }
}
