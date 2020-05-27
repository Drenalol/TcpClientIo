using System;
using Drenalol.Attributes;

namespace Drenalol.Exceptions
{
    public class TcpException : Exception
    {
        private TcpException(string message) : base(message)
        {
        }

        public static TcpException Throw(TcpTypeException typeException, params string[] someData)
        {
            switch (typeException)
            {
                case TcpTypeException.SerializerSequenceViolated:
                    return new TcpException($"Sequence violated in {nameof(TcpDataAttribute.Index)}");
                case TcpTypeException.SerializerLengthOutOfRange:
                    return new TcpException($"({someData[0]}, {someData[1]} bytes) is greater than attribute length {someData[2]} bytes");
                case TcpTypeException.PropertyArgumentIsNull:
                    return new TcpException($"NULL value cannot be converted ({someData[0]})");
                case TcpTypeException.PropertyCanReadWrite:
                    return new TcpException($"Set and Get keywords required for Serializtion. Type: {someData[0]}, {nameof(TcpDataType)}: {someData[1]}");
                case TcpTypeException.ConverterNotFoundType:
                    return new TcpException($"Not found converter for {someData[0]}");
                case TcpTypeException.ConverterUnknownError:
                    return new TcpException($"Error while trying convert data {someData[0]}, error: {someData[1]}");
                case TcpTypeException.AttributeKeyRequired:
                    return new TcpException($"{someData[0]} does not have required attribute {nameof(TcpDataType.Id)}");
                case TcpTypeException.AttributeBodyLengthRequired:
                    return new TcpException($"In {someData[0]} {nameof(TcpDataType.BodyLength)} could not work without {nameof(TcpDataType.Body)}");
                case TcpTypeException.AttributeBodyRequired:
                    return new TcpException($"In {someData[0]} {nameof(TcpDataType.Body)} could not work without {nameof(TcpDataType.BodyLength)}");
                case TcpTypeException.AttributeDuplicate:
                    return new TcpException($"{someData[0]} could not work with multiple {someData[1]}");
                case TcpTypeException.SerializerBodyIsEmpty:
                    return new TcpException($"{nameof(TcpDataType.Body)} is Empty");
                default:
                    return new TcpException(string.Empty);
            }
        }
    }
}