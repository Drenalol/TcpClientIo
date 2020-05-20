#nullable enable
using System;
using System.Reflection;

namespace Drenalol.Attributes
{
    public class TcpPackageProperty
    {
        public readonly bool IsValueType;
        private PropertyInfo Property { get; }
        public TcpPackageDataAttribute Attribute { get; }
        public Type PropertyType { get; }

        public TcpPackageProperty(PropertyInfo property, TcpPackageDataAttribute attribute, bool isValueType)
        {
            IsValueType = isValueType;
            Property = property;
            Attribute = attribute;
            PropertyType = attribute.Type ?? property.PropertyType;
        }

        public object? Get(object? tcpPackage) => Property.GetValue(tcpPackage);

        public object? SetInValueType(object? tcpPackage, object? @object)
        {
            Property.SetValue(tcpPackage, @object);
            return tcpPackage;
        }
        
        public void SetInClass(object? tcpPackage, object? @object) => Property.SetValue(tcpPackage, @object);

        internal bool CanReadWrite => Property.CanRead && Property.CanWrite;
    }
}