using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Extensions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpDeserializer<TId, TData> where TId : struct where TData : new()
    {
        private readonly ReflectionHelper _reflection;
        private readonly BitConverterHelper _bitConverter;

        public TcpDeserializer(BitConverterHelper bitConverterHelper)
        {
            _bitConverter = bitConverterHelper;
            _reflection = new ReflectionHelper(typeof(TData), null, bitConverterHelper, typeof(TId));
        }

        public async Task<(TId, TData)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
        {
            TData data;
            TId id;

            var metaReadResult = await pipeReader.ReadLengthAsync(_reflection.MetaLength, token);

            if (_reflection.LengthProperty == null)
            {
                var sequence = metaReadResult.Slice(_reflection.MetaLength);
                (id, data) = Deserialize(sequence);
                pipeReader.Consume(sequence.GetPosition(_reflection.MetaLength));
            }
            else
            {
                var lengthAttribute = _reflection.LengthProperty.Attribute;
                var lengthSequence = metaReadResult.Slice(lengthAttribute.Length, lengthAttribute.Index);
                var lengthValue = _bitConverter.ConvertFromBytes(lengthSequence, _reflection.LengthProperty.PropertyType, lengthAttribute.Reverse);
                var length = Convert.ToInt32(lengthValue);
                var totalLength = _reflection.MetaLength + length;
                ReadOnlySequence<byte> sequence;

                if (metaReadResult.Buffer.Length >= totalLength)
                    sequence = metaReadResult.Slice(totalLength);
                else
                {
                    pipeReader.Examine(metaReadResult.Buffer.Start, metaReadResult.Buffer.GetPosition(_reflection.MetaLength));
                    var totalReadResult = await pipeReader.ReadLengthAsync(totalLength, token);
                    sequence = totalReadResult.Slice(totalLength);
                }

                (id, data) = Deserialize(sequence, lengthValue);
                pipeReader.Consume(sequence.GetPosition(totalLength));
            }

            return (id, data);
        }

        public (TId, TData) Deserialize(in ReadOnlySequence<byte> sequence, object preKnownLength = null)
        {
            var data = new TData();
            TId id = default;

            var length = 0;
            var propertyIndex = 0;
            var examined = 0;

            foreach (var property in _reflection.Properties)
            {
                object value;
                int sliceLength;

                if (property.Attribute.TcpDataType == TcpDataType.Length && preKnownLength != null)
                {
                    value = preKnownLength;
                    length = Convert.ToInt32(preKnownLength);
                    sliceLength = property.Attribute.Length;
                    SetValue();
                    continue;
                }

                sliceLength = property.Attribute.TcpDataType switch
                {
                    TcpDataType.MetaData => property.Attribute.Length,
                    TcpDataType.Id => property.Attribute.Length,
                    TcpDataType.Length => property.Attribute.Length,
                    TcpDataType.Body => length,
                    TcpDataType.Compose => length,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var slice = sequence.Slice(propertyIndex, sliceLength);

                value = property.Attribute.TcpDataType == TcpDataType.Compose
                    ? property.Composition.Deserialize(slice)
                    : _bitConverter.ConvertFromBytes(slice, property.PropertyType, property.Attribute.Reverse);

                if (property.Attribute.TcpDataType == TcpDataType.Id)
                    id = (TId) value;
                else if (property.Attribute.TcpDataType == TcpDataType.Length)
                    length = Convert.ToInt32(value);

                SetValue();

                if (examined == _reflection.Properties.Count)
                    break;

                void SetValue()
                {
                    if (property.IsValueType)
                        data = (TData) property.Set(data, value);
                    else
                        property.Set(data, value);

                    propertyIndex += sliceLength;
                    examined++;
                }
            }

            return (id, data);
        }
    }
}