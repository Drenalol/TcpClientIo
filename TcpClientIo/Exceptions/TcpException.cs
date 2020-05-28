using System;
using Drenalol.Attributes;
using Drenalol.Extensions;
using Microsoft.Extensions.Logging;

namespace Drenalol.Exceptions
{
    public class TcpException : Exception
    {
        private TcpException(string message) : base(message)
        {
        }
        
        internal static TcpException Throw(TcpTypeException typeException, ILogger logger, params string[] someData)
        {
            switch (typeException)
            {
                case TcpTypeException.SerializerSequenceViolated:
                    return new TcpException($"Sequence violated in {nameof(TcpDataAttribute.Index)}").CaptureError(logger);
                case TcpTypeException.SerializerLengthOutOfRange:
                    return new TcpException($"({someData[0]}, {someData[1]} bytes) is greater than attribute length {someData[2]} bytes").CaptureError(logger);
                case TcpTypeException.PropertyArgumentIsNull:
                    return new TcpException($"NULL value cannot be converted ({someData[0]})").CaptureError(logger);
                case TcpTypeException.PropertyCanReadWrite:
                    return new TcpException($"Set and Get keywords required for Serializtion. Type: {someData[0]}, {nameof(TcpDataType)}: {someData[1]}").CaptureError(logger);
                case TcpTypeException.ConverterNotFoundType:
                    return new TcpException($"Not found converter for {someData[0]}").CaptureError(logger);
                case TcpTypeException.ConverterUnknownError:
                    return new TcpException($"Error while trying convert data {someData[0]}, error: {someData[1]}").CaptureError(logger);
                case TcpTypeException.AttributeKeyRequired:
                    return new TcpException($"{someData[0]} does not have required attribute {nameof(TcpDataType.Id)}").CaptureError(logger);
                case TcpTypeException.AttributeBodyLengthRequired:
                    return new TcpException($"In {someData[0]} {nameof(TcpDataType.BodyLength)} could not work without {nameof(TcpDataType.Body)}").CaptureError(logger);
                case TcpTypeException.AttributeBodyRequired:
                    return new TcpException($"In {someData[0]} {nameof(TcpDataType.Body)} could not work without {nameof(TcpDataType.BodyLength)}").CaptureError(logger);
                case TcpTypeException.AttributeDuplicate:
                    return new TcpException($"{someData[0]} could not work with multiple {someData[1]}").CaptureError(logger);
                case TcpTypeException.SerializerBodyIsEmpty:
                    return new TcpException($"{nameof(TcpDataType.Body)} is Empty").CaptureError(logger);
                default:
                    return new TcpException(string.Empty).CaptureError(logger);
            }
        }
    }
}