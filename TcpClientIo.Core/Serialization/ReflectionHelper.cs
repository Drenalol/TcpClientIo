using System.Reflection;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization;

internal class ReflectionHelper
{
    public IReadOnlyList<TcpProperty> Properties { get; }
    public int MetaLength { get; }
    public TcpProperty? BodyProperty { get; }
    public TcpProperty? LengthProperty { get; }

    public ReflectionHelper(Type typeData)
    {
        var attributed = GetTypePropertiesWithAttribute(typeData);
        EnsureTypeHasRequiredAttributes(typeData, attributed);
        Properties = attributed.Select(tuple => new TcpProperty(tuple.Property, tuple.Attribute, typeData)).ToList();
        MetaLength = Properties.Sum(p => p.Attribute.Length);
        LengthProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Length);
        BodyProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Body);
    }

    private static List<(PropertyInfo Property, TcpDataAttribute Attribute)> GetTypePropertiesWithAttribute(Type typeData) =>
        typeData
            .GetProperties()
            .Select(property => (Property: property, Attribute: GetTcpDataAttribute(property)))
            .Where(tuple => tuple.Attribute != null)
            .Select(tuple => (tuple.Property, tuple.Attribute!))
            .ToList();

    private static void EnsureTypeHasRequiredAttributes(Type typeData, IReadOnlyList<(PropertyInfo Property, TcpDataAttribute Attribute)> attributed)
    {
        var key = attributed.Where(item => item.Attribute.TcpDataType == TcpDataType.Id).ToList();

        if (key.Count > 1)
            throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Id));

        if (key.Count == 1 && !CanReadWrite(key[0].Property))
            throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Id));

        var body = attributed.Where(item => item.Attribute.TcpDataType == TcpDataType.Body).ToList();

        if (body.Count > 1)
            throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Body));

        if (body.Count == 1 && !CanReadWrite(body[0].Property))
            throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Body));

        var length = attributed.Where(item => item.Attribute.TcpDataType == TcpDataType.Length).ToList();

        if (length.Count > 1)
            throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Length));

        if (body.Count == 1)
        {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (length.Count == 0)
                throw TcpException.AttributeLengthRequired(typeData.ToString(), nameof(TcpDataType.Body));

            if (!CanReadWrite(length[0].Property))
                throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Length));
        }
        else if (length.Count == 1)
            throw TcpException.AttributeRequiredWithLength(typeData.ToString());

        var metaData = attributed.Where(item => item.Attribute.TcpDataType == TcpDataType.MetaData).ToList();

        if (key.Count == 0 && length.Count == 0 && body.Count == 0 && metaData.Count == 0)
            throw TcpException.AttributesRequired(typeData.ToString());

        var notReadWrite = metaData.FirstOrDefault(item => !CanReadWrite(item.Property));
        if (notReadWrite.Property != null)
            throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.MetaData), notReadWrite.Attribute.Index.ToString());
    }

    private static bool CanReadWrite(PropertyInfo property) => property.CanRead && property.CanWrite;

    private static TcpDataAttribute? GetTcpDataAttribute(MemberInfo property) => property.GetCustomAttribute<TcpDataAttribute>(true);
}
