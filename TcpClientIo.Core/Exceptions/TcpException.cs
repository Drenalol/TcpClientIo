using System;
using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Exceptions
{
    public class TcpException : Exception
    {
        private TcpException(string message) : base(message)
        {
        }

        public static TcpException SerializerSequenceViolated() =>
            new TcpException($"Sequence violated in {nameof(TcpDataAttribute.Index)}");

        public static TcpException SerializerLengthOutOfRange(string propertyName, string valueLength, string attributeLength) =>
            new TcpException($"({propertyName}, {valueLength} bytes) is greater than attribute length {attributeLength} bytes");

        public static TcpException PropertyArgumentIsNull(string propertyName) =>
            new TcpException($"NULL value cannot be converted ({propertyName})");

        public static TcpException PropertyCanReadWrite(string type, string attributeType, string attributeIndex = null) =>
            new TcpException($"Set and Get keywords required for Serializtion. Type: {type}, {nameof(TcpDataType)}: {attributeType}, {(attributeType == nameof(TcpDataType.MetaData) ? $"Index: {attributeIndex}" : null)}");

        public static TcpException ConverterNotFoundType(string propertyName) =>
            new TcpException($"Converter not found for {propertyName}");

        public static TcpException ConverterUnknownError(string propertyName, string errorMessage) =>
            new TcpException($"Error while trying convert data {propertyName}, error: {errorMessage}");

        public static TcpException AttributesRequired(string type) =>
            new TcpException($"{type} does not have any {nameof(TcpDataAttribute)}");

        public static TcpException AttributeBodyLengthRequired(string type) =>
            new TcpException($"In {type} {nameof(TcpDataType.BodyLength)} could not work without {nameof(TcpDataType.Body)}");

        public static TcpException AttributeBodyRequired(string type) =>
            new TcpException($"In {type} {nameof(TcpDataType.Body)} could not work without {nameof(TcpDataType.BodyLength)}");

        public static TcpException AttributeDuplicate(string type, string attributeType) =>
            new TcpException($"{type} could not work with multiple {attributeType}");

        public static TcpException SerializerBodyIsNull() =>
            new TcpException($"{nameof(TcpDataType.Body)} is Null");
    }
}