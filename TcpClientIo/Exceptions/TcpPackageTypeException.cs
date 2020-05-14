namespace Drenalol.Exceptions
{
    public enum TcpPackageTypeException
    {
        SerializerSequenceViolated,
        SerializerLengthOutOfRange,
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