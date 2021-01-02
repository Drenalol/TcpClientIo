using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class TcpSerializer<TId, TRequest, TResponse> where TResponse : new() where TId : struct
    {
        private readonly Func<int, byte[]> _byteArrayFactory;
        private readonly ReflectionHelper<TRequest, TResponse> _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public TcpSerializer(ICollection<TcpConverter> converters, Func<int, byte[]> byteArrayFactory)
        {
            _byteArrayFactory = byteArrayFactory;
            _reflectionHelper = new ReflectionHelper<TRequest, TResponse>();
            var preparedConverters = new Dictionary<Type, TcpConverter>();

            if (converters.Count > 0)
            {
                foreach (var converter in converters)
                {
                    var converterType = converter.GetType();
                    var type = converterType.BaseType;

                    if (type == null)
                        throw TcpClientIoException.ConverterError(converterType.Name);

                    var genericType = type.GenericTypeArguments.Single();
                    preparedConverters.Add(genericType, converter);
                }
            }

            _bitConverterHelper = new BitConverterHelper(preparedConverters);
        }

        public SerializedRequest Serialize(TRequest request)
        {
            int realLength;
            var serializedBody = new byte[0];
            var key = 0;
            var examined = 0;

            var properties = _reflectionHelper.RequestProperties;
            var bodyProperty = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.Body).Value;

            if (bodyProperty != null)
            {
                var bodyLengthProperty = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.BodyLength).Value;

                var bodyValue = bodyProperty.Get(request);
                serializedBody = _bitConverterHelper.ConvertToBytes(bodyValue, bodyProperty.PropertyType, bodyProperty.Attribute.Reverse);

                if (serializedBody == null)
                    throw TcpException.SerializerBodyIsNull();

                var lengthValue = bodyLengthProperty.PropertyType == typeof(int)
                    ? serializedBody.Length
                    : Convert.ChangeType(serializedBody.Length, bodyLengthProperty.PropertyType);

                if (bodyLengthProperty.IsValueType)
                    request = (TRequest) bodyLengthProperty.Set(request, lengthValue);
                else
                    bodyLengthProperty.Set(request, lengthValue);

                try
                {
                    realLength = (int) lengthValue + _reflectionHelper.RequestMetaLength;
                }
                catch (InvalidCastException)
                {
                    realLength = Convert.ToInt32(lengthValue) + _reflectionHelper.RequestMetaLength;
                }
            }
            else
                realLength = _reflectionHelper.RequestMetaLength;

            var rentedArray = _byteArrayFactory(realLength);

            while (properties.TryGetValue(key, out var property))
            {
                var value = property.Attribute.TcpDataType == TcpDataType.Body
                    ? serializedBody
                    : _bitConverterHelper.ConvertToBytes(property.Get(request), property.PropertyType, property.Attribute.Reverse);
                var valueLength = value.Length;

                if (property.Attribute.TcpDataType != TcpDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                var attributeLength = property.Attribute.TcpDataType == TcpDataType.Body ? valueLength : property.Attribute.Length;
                value.CopyTo(rentedArray, property.Attribute.Index);
                key += attributeLength;
                examined++;

                if (examined == properties.Count)
                    break;
            }

            return new SerializedRequest(rentedArray, realLength);
        }

        public async Task<(TId, TResponse)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
        {
            TResponse response;
            TId id;

            var bodyLengthProperty = _reflectionHelper.ResponseBodyLengthProperty;
            var responseMetaLength = _reflectionHelper.ResponseMetaLength;
            var metaReadResult = await pipeReader.ReadLengthAsync(responseMetaLength, token);

            if (bodyLengthProperty == null)
            {
                var responseSlice = metaReadResult.Slice(responseMetaLength);

                (id, response) = Deserialize(responseSlice);

                pipeReader.Consume(responseSlice.GetPosition(responseMetaLength));
            }
            else
            {
                var bodyLengthattribute = bodyLengthProperty.Attribute;
                var bodyLengthSequence = metaReadResult.Slice(bodyLengthattribute.Length, bodyLengthattribute.Index);
                var bodyLengthValue = _bitConverterHelper.ConvertFromBytes(bodyLengthSequence, bodyLengthProperty.PropertyType, bodyLengthattribute.Reverse);
                var bodyLength = Convert.ToInt32(bodyLengthValue);
                var totalLength = responseMetaLength + bodyLength;

                ReadOnlySequence<byte> responseSlice;
                
                if (metaReadResult.Buffer.Length >= totalLength)
                    responseSlice = metaReadResult.Slice(totalLength);
                else
                {
                    pipeReader.Examine(metaReadResult.Buffer.Start, metaReadResult.Buffer.GetPosition(responseMetaLength));
                    responseSlice = (await pipeReader.ReadLengthAsync(totalLength, token)).Slice(totalLength);
                }

                (id, response) = Deserialize(responseSlice, bodyLengthValue);

                pipeReader.Consume(responseSlice.GetPosition(totalLength));
            }

            return (id, response);
        }

        public (TId, TResponse) Deserialize(in ReadOnlySequence<byte> responseSequence, object preKnownBodyLength = null)
        {
            var response = new TResponse();
            TId id = default;

            var bodyLength = 0;
            var key = 0;
            var examined = 0;
            var properties = _reflectionHelper.ResponseProperties;

            while (properties.TryGetValue(key, out var property))
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

                var slice = responseSequence.Slice(key, sliceLength);
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
                        response = (TResponse) property.Set(response, value);
                    else
                        property.Set(response, value);

                    key += sliceLength;
                    examined++;
                }
            }

            return (id, response);
        }
    }
}