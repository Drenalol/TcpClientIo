using System;

namespace Drenalol.TcpClientIo.Converters
{
    public abstract class TcpConverter
    {
        public abstract byte[] ConvertTo(object input);
        public abstract object ConvertBackTo(ReadOnlySpan<byte> input);
    }
    
    public abstract class TcpConverter<T> : TcpConverter
    {
        public sealed override byte[] ConvertTo(object input)
        {
            if (input != null && input is T genericInput)
                return Convert(genericInput);

            throw new ArgumentException(nameof(input));
        }

        public abstract byte[] Convert(T input);

        public sealed override object ConvertBackTo(ReadOnlySpan<byte> input)
        {
            if (input != null)
                return ConvertBack(input);
            
            throw new ArgumentException(nameof(input));
        }
        
        public abstract T ConvertBack(ReadOnlySpan<byte> input);
    }
}