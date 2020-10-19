using System;

namespace Drenalol.TcpClientIo
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TcpDataAttribute : Attribute
    {
        /// <summary>
        /// Property position in Byte Array.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Property length in Byte Array. If TcpDataType set to TcpDataType.Body is ignored. Overwritten by the serializer.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Optional. Reverses the sequence of the elements in the serialized Byte Array.
        /// <para>Used for cases where the receiving side uses a different endianness.</para>
        /// </summary>
        public bool Reverse { get; set; }

        /// <summary>
        /// Sets the property type for the serializer.
        /// </summary>
        [Obsolete]
        public Type Type { get; set; }
        
        /// <summary>
        /// Sets the serialization rule for this property.
        /// </summary>
        public TcpDataType TcpDataType { get; set; }

        public TcpDataAttribute(int index, int length = 0, TcpDataType tcpDataType = TcpDataType.MetaData)
        {
            Index = index;
            Length = length;
            TcpDataType = tcpDataType;
        }
    }
}