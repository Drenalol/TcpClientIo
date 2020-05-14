#nullable enable
using System;
using System.Reflection;

namespace Drenalol.Attributes
{
    public class TcpPackageProperty
    {
        private PropertyInfo Property { get; }
        public TcpPackageDataAttribute Attribute { get; }
        public Type PropertyType { get; }

        public TcpPackageProperty(PropertyInfo property, TcpPackageDataAttribute attribute)
        {
            Property = property;
            Attribute = attribute;
            PropertyType = attribute.Type ?? property.PropertyType;
        }

        public object? Get(object? tcpPackage) => Property.GetValue(tcpPackage);
        
        public void Set(object? tcpPackage, object? @object) => Property.SetValue(tcpPackage, @object);
        
        internal bool CanReadWrite => Property.CanRead && Property.CanWrite;
    }
}