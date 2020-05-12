using System;

namespace Drenalol.Exceptions
{
    public class TcpClientIoException : Exception
    {
        private TcpClientIoException(string message) : base(message)
        {
        }

        public static TcpClientIoException Throw(TcpClientIoTypeException typeException, params string[] someData)
        {
            return typeException switch
            {
                TcpClientIoTypeException.InternalError => new TcpClientIoException($"Internal error handled {someData[0]}"),
                _ => new TcpClientIoException(string.Empty)
            };
        }
    }
}