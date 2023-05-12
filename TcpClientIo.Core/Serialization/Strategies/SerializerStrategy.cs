using System.Buffers;

namespace Drenalol.TcpClientIo.Serialization.Strategies
{
    internal record struct SerailizeResult(ReadOnlySequence<byte>? Data, int Length); 
    
    internal abstract class SerializerStrategy<TData> where TData : notnull
    {
        public abstract SerailizeResult GetBodyData(TData value);
    }
}