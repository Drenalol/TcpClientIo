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
        private readonly ReflectionHelper _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public TcpDeserializer(BitConverterHelper bitConverterHelper)
        {
            _bitConverterHelper = bitConverterHelper;
            _reflectionHelper = new ReflectionHelper(typeof(TData), null, bitConverterHelper);
        }
        
        public async Task<(TId, TData)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
        {
            TData data;
            TId id;

            var metaLength = _reflectionHelper.MetaLength;
            var metaReadResult = await pipeReader.ReadLengthAsync(metaLength, token);
            
            // TODO From there will be found compose properties and do magic before send it in Deserialize method

            //var composeProperties = _reflectionHelper.Properties.Where(p => p.IsCompose);

            if (_reflectionHelper.BodyLengthProperty == null)
            {
                var sequence = metaReadResult.Slice(metaLength);
                (id, data) = Deserialize(sequence);
                pipeReader.Consume(sequence.GetPosition(metaLength));
            }
            else
            {
                var bodyLengthAttribute = _reflectionHelper.BodyLengthProperty.Attribute;
                var bodyLengthSequence = metaReadResult.Slice(bodyLengthAttribute.Length, bodyLengthAttribute.Index);
                var bodyLengthValue = _bitConverterHelper.ConvertFromBytes(bodyLengthSequence, _reflectionHelper.BodyLengthProperty.PropertyType, bodyLengthAttribute.Reverse);
                var bodyLength = Convert.ToInt32(bodyLengthValue);
                var totalLength = metaLength + bodyLength;
                ReadOnlySequence<byte> sequence;
                
                if (metaReadResult.Buffer.Length >= totalLength)
                    sequence = metaReadResult.Slice(totalLength);
                else
                {
                    pipeReader.Examine(metaReadResult.Buffer.Start, metaReadResult.Buffer.GetPosition(metaLength));
                    var totalReadResult = await pipeReader.ReadLengthAsync(totalLength, token);
                    sequence = totalReadResult.Slice(totalLength);
                }

                (id, data) = Deserialize(sequence, bodyLengthValue);
                pipeReader.Consume(sequence.GetPosition(totalLength));
            }

            return (id, data);
        }

        public (TId, TData) Deserialize(in ReadOnlySequence<byte> sequence, object preKnownBodyLength = null)
        {
            var data = new TData();
            TId id = default;

            var bodyLength = 0;
            var propertyIndex = 0;
            var examined = 0;
            var properties = _reflectionHelper.Properties;

            foreach (var property in properties)
            {
                object value;
                int sliceLength;
                var isBodyLength = property.Attribute.TcpDataType == TcpDataType.BodyLength;

                if (isBodyLength && preKnownBodyLength != null)
                {
                    value = preKnownBodyLength;
                    bodyLength = Convert.ToInt32(preKnownBodyLength);
                    sliceLength = property.Attribute.Length;
                    SetValue();
                    continue;
                }

                var isId = property.Attribute.TcpDataType == TcpDataType.Id;
                var isBody = property.Attribute.TcpDataType == TcpDataType.Body;
                sliceLength = isBody ? bodyLength : property.Attribute.Length;

                var slice = sequence.Slice(propertyIndex, sliceLength);
                value = _bitConverterHelper.ConvertFromBytes(slice, property.PropertyType, property.Attribute.Reverse);

                if (isId)
                    id = (TId) value;
                else if (isBodyLength)
                    bodyLength = Convert.ToInt32(value);

                SetValue();

                if (examined == properties.Count)
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