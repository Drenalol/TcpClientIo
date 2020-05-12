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
            return typeException switch
            {
                TcpPackageTypeException.SerializerSequenceViolated => new TcpPackageException($"Sequence violated in {nameof(TcpPackageDataAttribute.Index)}"),
                TcpPackageTypeException.SerializerLengthOutOfRange => new TcpPackageException($"({someData[0]}, {someData[1]} bytes) is greater than attribute length {someData[2]} bytes"),
                TcpPackageTypeException.PropertyArgumentIsNull => new TcpPackageException($"NULL value cannot be converted ({someData[0]})"),
                TcpPackageTypeException.ConverterNotFoundType => new TcpPackageException($"Not found converter for {someData[0]}"),
                TcpPackageTypeException.ConverterUnknownError => new TcpPackageException($"Error while trying convert data {someData[0]}, error: {someData[1]}"),
                TcpPackageTypeException.AttributeKeyRequired => new TcpPackageException($"{someData[0]} does not have required attribute {nameof(TcpPackageDataType.Key)}"),
                TcpPackageTypeException.AttributeBodyLengthRequired => new TcpPackageException($"In {someData[0]} {nameof(TcpPackageDataType.BodyLength)} could not work without {nameof(TcpPackageDataType.Body)}"),
                TcpPackageTypeException.AttributeBodyRequired => new TcpPackageException($"In {someData[0]} {nameof(TcpPackageDataType.Body)} could not work without {nameof(TcpPackageDataType.BodyLength)}"),
                TcpPackageTypeException.AttributeDuplicate => new TcpPackageException($"{someData[0]} could not work with multiple {someData[1]}"),
                _ => new TcpPackageException(string.Empty)
            };
        }
    }
}