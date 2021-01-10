using System;
using System.Buffers;
using System.Reflection;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpComposition
    {
        private MethodInfo _serializerMethod;
        private MethodInfo _deserializerMethod;
        private readonly object _serializer;
        private readonly object _deserializer;

        public TcpComposition(Type composeType, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper)
        {
            var serializerType = typeof(TcpSerializer<>).MakeGenericType(composeType);
            var deserializerType = typeof(TcpDeserializer<,>).MakeGenericType(typeof(int), composeType);
            _serializerMethod = serializerType.GetMethod(nameof(TcpSerializer<TcpComposition>.Serialize));
            _deserializerMethod = deserializerType.GetMethod(nameof(TcpDeserializer<int, TcpComposition>.Deserialize));

            if (_serializerMethod == null)
                throw new NullReferenceException(nameof(_serializerMethod));
            
            if (_deserializerMethod == null)
                throw new NullReferenceException(nameof(_deserializerMethod));
            
            _serializer = Activator.CreateInstance(serializerType, bitConverterHelper, byteArrayFactory);
            _deserializer = Activator.CreateInstance(deserializerType, bitConverterHelper);
        }

        public TcpComposition()
        {
        }

        public SerializedRequest Serialize(object data)
            => (SerializedRequest) _serializerMethod.Invoke(_serializer, new[] {data});

        public (object, object) Deserialize(in ReadOnlySequence<byte> sequence, object preKnownBodyLength = null)
            => ((object, object)) _deserializerMethod.Invoke(_deserializer, new[] {sequence, preKnownBodyLength});
    }
}