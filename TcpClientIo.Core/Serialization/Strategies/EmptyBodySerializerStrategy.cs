namespace Drenalol.TcpClientIo.Serialization.Strategies;

internal class EmptyBodySerializerStrategy<TData>(ReflectionHelper reflectionHelper) : SerializerStrategy<TData> where TData : notnull
{
    public override SerializeResult GetBodyData(TData value) => new(null, reflectionHelper.MetaLength, null);
}
