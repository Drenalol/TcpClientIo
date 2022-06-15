using System;
using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Exceptions
{
    /// <summary>
    /// Represents errors that occur during TcpClientIo execution.
    /// </summary>
    public class TcpException : Exception
    {
        private TcpException(string message) : base(message)
        {
        }

        internal static TcpException SerializerSequenceViolated() => new($"Sequence violated in {nameof(TcpDataAttribute.Index)}");

        internal static TcpException SerializerLengthOutOfRange(string propertyName, string valueLength, string attributeLength) => new($"({propertyName}, {valueLength} bytes) is greater than attribute length {attributeLength} bytes");

        internal static TcpException PropertyArgumentIsNull(string propertyName) => new($"NULL value cannot be converted ({propertyName})");

        internal static TcpException PropertyCanReadWrite(string type, string attributeType, string? attributeIndex = null) =>
            new($"Set and Get keywords required for Serializtion. Type: {type}, {nameof(TcpDataType)}: {attributeType}, {(attributeType == nameof(TcpDataType.MetaData) ? $"Index: {attributeIndex}" : null)}");

        internal static TcpException ConverterNotFoundType(string propertyName) => new($"Converter not found for {propertyName}");

        internal static TcpException ConverterUnknownError(string propertyName, string errorMessage) => new($"Error while trying convert data {propertyName}, error: {errorMessage}");

        internal static TcpException AttributesRequired(string type) => new($"{type} does not have any {nameof(TcpDataAttribute)}");

        internal static TcpException AttributeLengthRequired(string type, string attribute) => new($"In {type} {nameof(TcpDataType)}.{attribute} could not work without {nameof(TcpDataType)}.{nameof(TcpDataType.Length)}");

        internal static TcpException AttributeRequiredWithLength(string type) => new($"In {type} {nameof(TcpDataType)}.{nameof(TcpDataType.Length)} could not work without {nameof(TcpDataType)}.{nameof(TcpDataType.Body)}");

        internal static TcpException AttributeDuplicate(string type, string attributeType) => new($"{type} could not work with multiple {attributeType}");

        internal static TcpException SerializerBodyPropertyIsNull() => new($"Value of {nameof(TcpDataType)}.{nameof(TcpDataType.Body)} is Null");
    }
}