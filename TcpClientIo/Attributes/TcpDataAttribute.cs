using System;

namespace Drenalol.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TcpDataAttribute : Attribute
    {
        public int Index { get; }
        public int Length { get; }
        public bool Reverse { get; set; }
        public Type Type { get; set; }
        public TcpDataType TcpDataType { get; set; }
        
        public TcpDataAttribute(int index, int length = 0)
        {
            Index = index;
            Length = length;
        }
    }
}