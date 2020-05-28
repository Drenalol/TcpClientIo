#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
#nullable enable
#endif
using System;
using System.Reflection;

namespace Drenalol.Attributes
{
    internal class TcpProperty
    {
        public readonly bool IsValueType;
        private PropertyInfo Property { get; }
        public TcpDataAttribute Attribute { get; }
        public Type PropertyType { get; }

        public TcpProperty(PropertyInfo property, TcpDataAttribute attribute, bool isValueType)
        {
            IsValueType = isValueType;
            Property = property;
            Attribute = attribute;
            PropertyType = attribute.Type ?? property.PropertyType;
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public object? Get(object? input) => Property.GetValue(input);
#else
        public object Get(object input) => Property.GetValue(input);
#endif

#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public object? SetInValueType(object? input, object? @object)
#else
        public object SetInValueType(object input, object @object)
#endif
        {
            Property.SetValue(input, @object);
            return input;
        }
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            public void SetInClass(object? input, object? @object) => Property.SetValue(input, @object);
#else
            public void SetInClass(object input, object @object) => Property.SetValue(input, @object);
#endif

        internal bool CanReadWrite => Property.CanRead && Property.CanWrite;
    }
}