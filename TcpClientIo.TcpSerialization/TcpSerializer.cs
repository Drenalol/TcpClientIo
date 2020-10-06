using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TcpClientIo.Abstractions;
using TcpClientIo.Attributes;
using TcpClientIo.Converters;
using TcpClientIo.Exceptions;
using TcpClientIo.Extensions;

namespace TcpClientIo.Serialization
{
    public class TcpSerializer<TRequest, TResponse> where TResponse : new()
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
                    var type = converter.GetType().BaseType;

                    if (type == null)
                        throw TcpClientIoException.Throw(TcpClientIoTypeException.ConverterError);

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
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
            var (_, bodyValue) = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.Body);
#else
            var bodyProperty = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.Body);
            var bodyValue = bodyProperty.Value;
#endif
            
            if (bodyValue != null)
            {
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
                var (_, bodyLengthValue) = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.BodyLength);
#else
                var bodyLengthProperty = properties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.BodyLength);
                var bodyLengthValue = bodyLengthProperty.Value;
#endif

                if (bodyValue.PropertyType == typeof(byte[]))
                {
                    var bytes = (byte[]) bodyValue.Get(request);
                    serializedBody = bodyValue.Attribute.Reverse ? _bitConverterHelper.Reverse(bytes) : bytes;
                }
                else
                    serializedBody = _bitConverterHelper.ConvertToBytes(bodyValue.Get(request), bodyValue.PropertyType, bodyValue.Attribute.Reverse);

                if (serializedBody == null)
                    throw TcpException.Throw(TcpTypeException.SerializerBodyIsEmpty);

                if (bodyLengthValue.IsValueType)
                    request = (TRequest) bodyLengthValue.SetInValueType(request, bodyLengthValue.PropertyType == typeof(int) ? serializedBody.Length : Convert.ChangeType(serializedBody.Length, bodyLengthValue.PropertyType));
                else
                    bodyLengthValue.SetInClass(request, bodyLengthValue.PropertyType == typeof(int) ? serializedBody.Length : Convert.ChangeType(serializedBody.Length, bodyLengthValue.PropertyType));
            }

            while (properties.TryGetValue(key, out var property))
            {
                var value = property.Attribute.TcpDataType == TcpDataType.Body
                    ? serializedBody
                    : _bitConverterHelper.ConvertToBytes(property.Get(request), property.PropertyType, property.Attribute.Reverse);
                var valueLength = value.Length;

                if (property.Attribute.TcpDataType != TcpDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpException.Throw(TcpTypeException.SerializerLengthOutOfRange, property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                var attributeLength = property.Attribute.TcpDataType == TcpDataType.Body ? valueLength : property.Attribute.Length;

                if (property.Attribute.TcpDataType == TcpDataType.MetaData && valueLength < attributeLength)
                    Array.Resize(ref value, attributeLength);

                Array.Resize(ref serializedRequest, property.Attribute.Index + attributeLength);
                Array.Copy(value, 0, serializedRequest, property.Attribute.Index, attributeLength);
                key += attributeLength;
                examined++;
                Trace.WriteLine($"Serialize property {property.Attribute.TcpDataType.ToString()} Index: {property.Attribute.Index.ToString()} {valueLength.ToString()}/{property.Attribute.Length.ToString()} bytes");
            }

            if (examined != properties.Count)
                throw TcpException.Throw(TcpTypeException.SerializerSequenceViolated);

            return serializedRequest;
        }

        public async Task<(object, int, TResponse)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
        {
            var response = new TResponse();
            var key = 0;
            var examined = 0;
            object tcpId = null;
            var tcpBodyLength = 0;
            var properties = _reflectionHelper.GetResponseProperties();

            while (properties.TryGetValue(key, out var property))
            {
                var sliceLength = 0;

                switch (property.Attribute.TcpDataType)
                {
                    case TcpDataType.Id:
                    case TcpDataType.BodyLength:
                    case TcpDataType.MetaData:
                        sliceLength = property.Attribute.Length;
                        break;
                    case TcpDataType.Body:
                        sliceLength = tcpBodyLength;
                        break;
                }

                var bytesFromReader = await pipeReader.ReadLengthAsync(sliceLength, token);
                object value = null;

                switch (property.Attribute.TcpDataType)
                {
                    case TcpDataType.MetaData:
                        value = _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpDataType.Body:
                        value = property.PropertyType == typeof(byte[])
                            ? property.Attribute.Reverse ? _bitConverterHelper.Reverse(bytesFromReader) : bytesFromReader
                            : _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpDataType.Id:
                        value = _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        tcpId = value;
                        break;
                    case TcpDataType.BodyLength:
                        value = _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        tcpBodyLength = Convert.ToInt32(value);
                        break;
                }

                if (property.IsValueType)
                    response = (TResponse) property.SetInValueType(response, value);
                else
                    property.SetInClass(response, value);

                key += sliceLength;
                examined++;
                Trace.WriteLine($"Deserialize property {property.Attribute.TcpDataType.ToString()} Index: {property.Attribute.Index.ToString()} {sliceLength.ToString()}/{property.Attribute.Length.ToString()} bytes");
            }

            if (examined != properties.Count)
                throw TcpException.Throw(TcpTypeException.SerializerSequenceViolated);

            return (tcpId ?? TcpClientIoBase.Unassigned, tcpBodyLength, response);
        }
    }
}