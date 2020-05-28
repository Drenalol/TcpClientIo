namespace Drenalol.Exceptions
{
    internal enum TcpTypeException
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