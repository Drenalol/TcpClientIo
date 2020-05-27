namespace Drenalol.Exceptions
{
    public enum TcpTypeException
    {
        SerializerSequenceViolated,
        SerializerLengthOutOfRange,
        SerializerBodyIsEmpty,
        PropertyArgumentIsNull,
        PropertyCanReadWrite,
        ConverterNotFoundType,
        ConverterUnknownError,
        AttributeKeyRequired,
        AttributeBodyRequired,
        AttributeBodyLengthRequired,
        AttributeDuplicate
    }
}