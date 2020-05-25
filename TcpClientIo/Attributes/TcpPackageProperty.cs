#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
#nullable enable
#endif
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
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public object? Get(object? tcpPackage) => Property.GetValue(tcpPackage);
#else
        public object Get(object tcpPackage) => Property.GetValue(tcpPackage);
#endif

#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public object? SetInValueType(object? tcpPackage, object? @object)
#else
        public object SetInValueType(object tcpPackage, object @object)
#endif
        {
            Property.SetValue(tcpPackage, @object);
            return tcpPackage;
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            public void SetInClass(object? tcpPackage, object? @object) => Property.SetValue(tcpPackage, @object);
#else
            public void SetInClass(object tcpPackage, object @object) => Property.SetValue(tcpPackage, @object);
#endif

        internal bool CanReadWrite => Property.CanRead && Property.CanWrite;
    }
}