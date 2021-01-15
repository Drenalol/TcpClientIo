using System;
using System.Buffers;
using System.Reflection;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpComposition
    {
        private MethodInfo _serializerMethod;
        private MethodInfo _deserializerMethod;
        private FieldInfo _deserializerField;
        private readonly object _serializer;
        private readonly object _deserializer;

        public TcpComposition(Type dataType, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper, Type idType = null)
        {
            var serializerType = typeof(TcpSerializer<>).MakeGenericType(dataType);
            _serializerMethod = serializerType.GetMethod(nameof(TcpSerializer<TcpComposition>.Serialize));
            
            if (_serializerMethod == null)
                throw new NullReferenceException(nameof(_serializerMethod));
            
            _serializer = Activator.CreateInstance(serializerType, bitConverterHelper, byteArrayFactory);
            
            if (idType == null)
                return;
            
            var deserializerType = typeof(TcpDeserializer<,>).MakeGenericType(idType, dataType);
            _deserializerMethod = deserializerType.GetMethod(nameof(TcpDeserializer<int, TcpComposition>.Deserialize));
            _deserializerField = typeof(ValueTuple<,>).MakeGenericType(idType, dataType).GetField("Item2");
            
            if (_deserializerMethod == null)
                throw new NullReferenceException(nameof(_deserializerMethod));
            
            _deserializer = Activator.CreateInstance(deserializerType, bitConverterHelper);
        }

        public TcpComposition()
        {
        }

        public SerializedRequest Serialize(object data)
            => (SerializedRequest) _serializerMethod.Invoke(_serializer, new[] {data});

        public object Deserialize(in ReadOnlySequence<byte> sequence, object preKnownLength = null)
        {
            var tuple = _deserializerMethod.Invoke(_deserializer, new[] {sequence, preKnownLength});
            return _deserializerField.GetValue(tuple);
        }
    }
}