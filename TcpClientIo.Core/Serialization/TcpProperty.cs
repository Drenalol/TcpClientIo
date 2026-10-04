using System.Reflection;
using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Serialization;

internal class TcpProperty(PropertyInfo propertyInfo, TcpDataAttribute attribute, Type accessorType)
{
    public TcpDataAttribute Attribute { get; } = attribute;
    public bool IsValueType { get; } = accessorType.IsValueType;
    public Type PropertyType { get; } = propertyInfo.PropertyType;

    public object? Get(object input) => propertyInfo.GetValue(input);

    public object Set(object input, object value)
    {
        propertyInfo.SetValue(input, value);
        return input;
    }
}
