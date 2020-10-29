using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    public class TcpSerializer<TId, TRequest, TResponse> where TResponse : new() where TId : struct
    {
        private readonly ReflectionHelper<TRequest, TResponse> _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public TcpSerializer(IReadOnlyCollection<TcpConverter> converters)
        {
            _reflectionHelper = new ReflectionHelper<TRequest, TResponse>();
            var immutableConverters = ImmutableDictionary<Type, TcpConverter>.Empty;

            if (converters.Count > 0)
            {
                var tempDict = new Dictionary<Type, TcpConverter>();

                foreach (var converter in converters)
                {
                    var converterType = converter.GetType();
                    var type = converterType.BaseType;

                    if (type == null)
                        throw TcpClientIoException.ConverterError(converterType.Name);

                    var genericType = type.GenericTypeArguments.Single();
                    tempDict.Add(genericType, converter);
                }

                immutableConverters = tempDict.ToImmutableDictionary();
            }

            _bitConverterHelper = new BitConverterHelper(immutableConverters);
        }

        public byte[] Serialize(TRequest request)
        {
            var serializedRequest = new byte[0];
            var serializedBody = new byte[0];
            var key = 0;
            var examined = 0;

            var properties = _reflectionHelper.GetRequestProperties();
            var bodyProperty = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.Body).Value;

            if (bodyProperty != null)
            {
                var bodyLengthProperty = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.BodyLength).Value;

                if (bodyProperty.PropertyType == typeof(byte[]))
                {
                    var bodyBytes = (byte[]) bodyProperty.Get(request);
                    serializedBody = bodyProperty.Attribute.Reverse ? _bitConverterHelper.Reverse(bodyBytes) : bodyBytes;
                }
                else
                {
                    var bodyValue = bodyProperty.Get(request);
                    serializedBody = _bitConverterHelper.ConvertToBytes(bodyValue, bodyProperty.PropertyType, bodyProperty.Attribute.Reverse);
                }

                if (serializedBody == null)
                    throw TcpException.SerializerBodyIsNull();

                var lengthValue = bodyLengthProperty.PropertyType == typeof(int)
                    ? serializedBody.Length
                    : Convert.ChangeType(serializedBody.Length, bodyLengthProperty.PropertyType);

                if (bodyLengthProperty.IsValueType)
                    request = (TRequest) bodyLengthProperty.Set(request, lengthValue);
                else
                    bodyLengthProperty.Set(request, lengthValue);
            }

            while (properties.TryGetValue(key, out var property))
            {
                var value = property.Attribute.TcpDataType == TcpDataType.Body
                    ? serializedBody
                    : _bitConverterHelper.ConvertToBytes(property.Get(request), property.PropertyType, property.Attribute.Reverse);
                var valueLength = value.Length;

                if (property.Attribute.TcpDataType != TcpDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpException.SerializerLengthOutOfRange(property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                var attributeLength = property.Attribute.TcpDataType == TcpDataType.Body ? valueLength : property.Attribute.Length;

                if (property.Attribute.TcpDataType == TcpDataType.MetaData && valueLength < attributeLength)
                    Array.Resize(ref value, attributeLength);

                Array.Resize(ref serializedRequest, property.Attribute.Index + attributeLength);
                Array.Copy(value, 0, serializedRequest, property.Attribute.Index, attributeLength);
                key += attributeLength;
                examined++;
                Trace.WriteLine($"Serialize property {property.Attribute.TcpDataType.ToString()} Index: {property.Attribute.Index.ToString()} {valueLength.ToString()}/{property.Attribute.Length.ToString()} bytes");

                if (examined == properties.Count)
                    break;
            }

            return serializedRequest;
        }

        public async Task<(TId, int, TResponse)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
        {
            var response = new TResponse();
            var key = 0;
            var examined = 0;
            TId id = default;
            var bodyLength = 0;
            var properties = _reflectionHelper.GetResponseProperties();

            while (properties.TryGetValue(key, out var property))
            {
                int sliceLength;
                byte[] bytes;

                if (property.Attribute.TcpDataType == TcpDataType.Body)
                {
                    sliceLength = bodyLength;
                    bytes = sliceLength == 0 ? new byte[0] : await pipeReader.ReadLengthAsync(sliceLength, token);
                }
                else
                {
                    sliceLength = property.Attribute.Length;
                    bytes = await pipeReader.ReadLengthAsync(sliceLength, token);
                }
                
                object value = null;

                switch (property.Attribute.TcpDataType)
                {
                    case TcpDataType.MetaData:
                        value = _bitConverterHelper.ConvertFromBytes(bytes, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpDataType.Body:
                        value = property.PropertyType == typeof(byte[])
                            ? property.Attribute.Reverse ? _bitConverterHelper.Reverse(bytes) : bytes
                            : _bitConverterHelper.ConvertFromBytes(bytes, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpDataType.Id:
                        value = _bitConverterHelper.ConvertFromBytes(bytes, property.PropertyType, property.Attribute.Reverse);
                        id = (TId) value;
                        break;
                    case TcpDataType.BodyLength:
                        value = _bitConverterHelper.ConvertFromBytes(bytes, property.PropertyType, property.Attribute.Reverse);
                        bodyLength = Convert.ToInt32(value);
                        break;
                }

                if (property.IsValueType)
                    response = (TResponse) property.Set(response, value);
                else
                    property.Set(response, value);

                key += sliceLength;
                examined++;
                Trace.WriteLine($"Deserialize property {property.Attribute.TcpDataType.ToString()} Index: {property.Attribute.Index.ToString()} {sliceLength.ToString()}/{property.Attribute.Length.ToString()} bytes");

                if (examined == properties.Count)
                    break;
            }

            return (id, bodyLength, response);
        }
    }
}