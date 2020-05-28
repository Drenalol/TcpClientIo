using System;
using Drenalol.Extensions;
using Microsoft.Extensions.Logging;

namespace Drenalol.Exceptions
{
    public class TcpClientIoException : Exception
    {
        private TcpClientIoException(string message) : base(message)
        {
        }
        
        internal static TcpClientIoException Throw(TcpClientIoTypeException typeException, ILogger logger, params string[] someData)
        {
            switch (typeException)
            {
                case TcpClientIoTypeException.InternalError:
                    return new TcpClientIoException($"Internal error handled {someData[0]}").CaptureError(logger);
                case TcpClientIoTypeException.ConverterError:
                    return new TcpClientIoException($"Converter {someData[0]} does not have generic").CaptureError(logger);
                default:
                    return new TcpClientIoException(string.Empty).CaptureError(logger);
            }
        }
    }
}