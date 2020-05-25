using System;
using Drenalol.Attributes;

namespace Drenalol.Exceptions
{
    public class TcpPackageException : Exception
    {
        private TcpPackageException(string message) : base(message)
        {
        }

        public static TcpPackageException Throw(TcpPackageTypeException typeException, params string[] someData)
        {
            switch (typeException)
            {
                case TcpPackageTypeException.SerializerSequenceViolated:
                    return new TcpPackageException($"Sequence violated in {nameof(TcpPackageDataAttribute.Index)}");
                case TcpPackageTypeException.SerializerLengthOutOfRange:
                    return new TcpPackageException($"({someData[0]}, {someData[1]} bytes) is greater than attribute length {someData[2]} bytes");
                case TcpPackageTypeException.PropertyArgumentIsNull:
                    return new TcpPackageException($"NULL value cannot be converted ({someData[0]})");
                case TcpPackageTypeException.PropertyCanReadWrite:
                    return new TcpPackageException($"Set and Get keywords required for Serializtion. Type: {someData[0]}, {nameof(TcpPackageDataType)}: {someData[1]}");
                case TcpPackageTypeException.ConverterNotFoundType:
                    return new TcpPackageException($"Not found converter for {someData[0]}");
                case TcpPackageTypeException.ConverterUnknownError:
                    return new TcpPackageException($"Error while trying convert data {someData[0]}, error: {someData[1]}");
                case TcpPackageTypeException.AttributeKeyRequired:
                    return new TcpPackageException($"{someData[0]} does not have required attribute {nameof(TcpPackageDataType.Id)}");
                case TcpPackageTypeException.AttributeBodyLengthRequired:
                    return new TcpPackageException($"In {someData[0]} {nameof(TcpPackageDataType.BodyLength)} could not work without {nameof(TcpPackageDataType.Body)}");
                case TcpPackageTypeException.AttributeBodyRequired:
                    return new TcpPackageException($"In {someData[0]} {nameof(TcpPackageDataType.Body)} could not work without {nameof(TcpPackageDataType.BodyLength)}");
                case TcpPackageTypeException.AttributeDuplicate:
                    return new TcpPackageException($"{someData[0]} could not work with multiple {someData[1]}");
                case TcpPackageTypeException.SerializerBodyIsEmpty:
                    return new TcpPackageException($"{nameof(TcpPackageDataType.Body)} is Empty");
                default:
                    return new TcpPackageException(string.Empty);
            }
        }
    }
}