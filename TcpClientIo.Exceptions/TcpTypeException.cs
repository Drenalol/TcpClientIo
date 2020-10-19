namespace Drenalol.TcpClientIo
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
        AttributesRequired,
        AttributeBodyRequired,
        AttributeBodyLengthRequired,
        AttributeDuplicate
    }
}