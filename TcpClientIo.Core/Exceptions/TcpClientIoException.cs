using System;

namespace Drenalol.TcpClientIo.Exceptions
{
    public class TcpClientIoException : Exception
    {
        private TcpClientIoException(string message) : base(message)
        {
        }

        public static TcpClientIoException ConverterError(string converterName) => new TcpClientIoException($"Converter {converterName} does not have generic type");
        
        public static readonly TcpClientIoException ConnectionBroken = new TcpClientIoException("Connection was broken");
    }
}