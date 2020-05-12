namespace Drenalol.Exceptions
{
    public enum TcpPackageTypeException
    {
        SerializerSequenceViolated,
        SerializerLengthOutOfRange,
        PropertyArgumentIsNull,
        ConverterNotFoundType,
        ConverterUnknownError,
        AttributeKeyRequired,
        AttributeBodyRequired,
        AttributeBodyLengthRequired,
        AttributeDuplicate
    }
}