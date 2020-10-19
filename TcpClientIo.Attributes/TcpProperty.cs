using System;
using System.Reflection;
using FastMember;

namespace Drenalol.TcpClientIo
{
    public class TcpProperty
    {
        private readonly TypeAccessor _accessor;
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
            _accessor = TypeAccessor.Create(accessorType);
        }

        public object Get(object input) => _accessor[input, _propertyInfo.Name];

        public object Set(object input, object value)
        {
            _accessor[input, _propertyInfo.Name] = value;
            return input;
        }
    }
}