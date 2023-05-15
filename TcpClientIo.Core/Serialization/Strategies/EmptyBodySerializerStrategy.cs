namespace Drenalol.TcpClientIo.Serialization.Strategies
{
    internal class EmptyBodySerializerStrategy<TData> : SerializerStrategy<TData> where TData : notnull
    {
        private readonly ReflectionHelper _reflectionHelper;

        public EmptyBodySerializerStrategy(ReflectionHelper reflectionHelper) => _reflectionHelper = reflectionHelper;

        public override SerailizeResult GetBodyData(TData value) => new(null, _reflectionHelper.MetaLength);
    }
}