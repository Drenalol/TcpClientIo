using System;

namespace Drenalol.TcpClientIo.Serialization
{
    /// <summary>
    /// Structure containing a span and an Delegate that will need to be invoke when the work with the span is completed.<para></para>
    /// If you invoked before working with the span, data will be lost.
    /// </summary>
    internal readonly ref struct Sequence
    {
        private readonly ReadOnlySpan<byte> _span;
        private readonly Action _returnRentedArray;
        
        private Sequence(ReadOnlySpan<byte> span, Action returnRentedArray)
        {
            _span = span;
            _returnRentedArray = returnRentedArray;
        }
        
        public static Sequence Create(ReadOnlySpan<byte> span, Action returnRentedArray) => new Sequence(span, returnRentedArray);

        /// <summary>
        /// Deconstruction method
        /// </summary>
        /// <param name="span"></param>
        /// <param name="returnRentedArray"></param>
        public void Deconstruct(out ReadOnlySpan<byte> span, out Action returnRentedArray)
        {
            span = _span;
            returnRentedArray = _returnRentedArray;
        }
    }
}