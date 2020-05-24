namespace Drenalol.Exceptions
{
    public enum TcpPackageTypeException
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