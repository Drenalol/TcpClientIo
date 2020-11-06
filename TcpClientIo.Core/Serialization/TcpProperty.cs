using System;
using System.Reflection;
using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpProperty
    {
        private readonly PropertyInfo _propertyInfo;

        public TcpDataAttribute Attribute { get; }
        public bool IsValueType { get; }
        public Type PropertyType => _propertyInfo.PropertyType;
        public bool CanReadWrite => _propertyInfo.CanRead && _propertyInfo.CanWrite;

        public TcpProperty(PropertyInfo propertyInfo, TcpDataAttribute attribute, Type accessorType)
        {
            Attribute = attribute;
            IsValueType = accessorType.IsValueType;
            _propertyInfo = propertyInfo;
        }

        public object Get(object input) => _propertyInfo.GetValue(input);

        public object Set(object input, object value)
        {
            _propertyInfo.SetValue(input, value);
            return input;
        }
    }
}