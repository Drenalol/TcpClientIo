namespace Drenalol.TcpClientIo.Exceptions
{
    public enum TcpTypeException
    {
        SerializerSequenceViolated,
        SerializerLengthOutOfRange,
        SerializerBodyIsNull,
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