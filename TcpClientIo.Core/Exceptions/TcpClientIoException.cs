using System;

namespace Drenalol.TcpClientIo.Exceptions
{
    public class TcpClientIoException : Exception
    {
        private TcpClientIoException(string message) : base(message)
        {
        }
        
        public static TcpClientIoException Throw(TcpClientIoTypeException typeException, params string[] someData)
        {
            switch (typeException)
            {
                case TcpClientIoTypeException.InternalError:
                    return new TcpClientIoException($"Internal error handled {someData[0]}");
                case TcpClientIoTypeException.ConverterError:
                    return new TcpClientIoException($"Converter {someData[0]} does not have generic");
                default:
                    return new TcpClientIoException(string.Empty);
            }
        }
    }
}