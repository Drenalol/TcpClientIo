using System;

namespace Drenalol.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TcpPackageDataAttribute : Attribute
    {
        public int Index { get; }
        public int Length { get; }
        public bool Reverse { get; set; }
        public Type Type { get; set; }
        public TcpPackageDataType AttributeData { get; set; }
        
        public TcpPackageDataAttribute(int index, int length)
        {
            Index = index;
            Length = length;
        }
    }
}