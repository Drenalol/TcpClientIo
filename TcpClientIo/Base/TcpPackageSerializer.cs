using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Attributes;
using Drenalol.Exceptions;
using Drenalol.Extensions;
using Drenalol.Helpers;

namespace Drenalol.Base
{
    internal class TcpPackageSerializer<TRequest, TResponse> where TResponse : new()
    {
        private readonly ReflectionHelper<TRequest, TResponse> _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public TcpPackageSerializer(ICollection<TcpPackageConverter> converters)
        {
            _reflectionHelper = new ReflectionHelper<TRequest, TResponse>();
            var immutableConverters = ImmutableDictionary<Type, TcpPackageConverter>.Empty;

            if (converters.Count > 0)
            {
                var tempDict = new Dictionary<Type, TcpPackageConverter>();

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
            var (_, bodyValue) = properties.SingleOrDefault(p => p.Value.Attribute.AttributeData == TcpPackageDataType.Body);
#else
            var bodyProperty = properties.SingleOrDefault(p => p.Value.Attribute.AttributeData == TcpPackageDataType.Body);
            var bodyValue = bodyProperty.Value;
#endif
            
            if (bodyValue != null)
            {
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
                var (_, bodyLengthValue) = properties.SingleOrDefault(p => p.Value.Attribute.AttributeData == TcpPackageDataType.BodyLength);
#else
                var bodyLengthProperty = properties.SingleOrDefault(p => p.Value.Attribute.AttributeData == TcpPackageDataType.BodyLength);
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
                    throw TcpPackageException.Throw(TcpPackageTypeException.SerializerBodyIsEmpty);

                if (bodyLengthValue.IsValueType)
                    request = (TRequest) bodyLengthValue.SetInValueType(request, bodyLengthValue.PropertyType == typeof(int) ? serializedBody.Length : Convert.ChangeType(serializedBody.Length, bodyLengthValue.PropertyType));
                else
                    bodyLengthValue.SetInClass(request, bodyLengthValue.PropertyType == typeof(int) ? serializedBody.Length : Convert.ChangeType(serializedBody.Length, bodyLengthValue.PropertyType));
            }

            while (properties.TryGetValue(key, out var property))
            {
                var value = property.Attribute.AttributeData == TcpPackageDataType.Body
                    ? serializedBody
                    : _bitConverterHelper.ConvertToBytes(property.Get(request), property.PropertyType, property.Attribute.Reverse);
                var valueLength = value.Length;

                if (property.Attribute.AttributeData != TcpPackageDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpPackageException.Throw(TcpPackageTypeException.SerializerLengthOutOfRange, property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                var attributeLength = property.Attribute.AttributeData == TcpPackageDataType.Body ? valueLength : property.Attribute.Length;

                if (property.Attribute.AttributeData == TcpPackageDataType.MetaData && valueLength < attributeLength)
                    Array.Resize(ref value, attributeLength);

                Array.Resize(ref serializedRequest, property.Attribute.Index + attributeLength);
                Array.Copy(value, 0, serializedRequest, property.Attribute.Index, attributeLength);
                key += attributeLength;
                examined++;
                //Debug.WriteLine($"Serialize property {valueLength.ToString()} bytes, {property.Attribute.AttributeData.ToString()}:{property.Attribute.Index.ToString()}:{property.Attribute.Length.ToString()}");
            }

            if (examined != properties.Count)
                throw TcpPackageException.Throw(TcpPackageTypeException.SerializerSequenceViolated);

            return serializedRequest;
        }

        public async Task<(object, int, TResponse)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
        {
            var response = new TResponse();
            var key = 0;
            var examined = 0;
            object tcpPackageId = null;
            var tcpPackageBodyLength = 0;
            var properties = _reflectionHelper.GetResponseProperties();

            while (properties.TryGetValue(key, out var property))
            {
                var sliceLength = 0;

                switch (property.Attribute.AttributeData)
                {
                    case TcpPackageDataType.Id:
                    case TcpPackageDataType.BodyLength:
                    case TcpPackageDataType.MetaData:
                        sliceLength = property.Attribute.Length;
                        break;
                    case TcpPackageDataType.Body:
                        sliceLength = tcpPackageBodyLength;
                        break;
                }

                var bytesFromReader = await pipeReader.ReadLengthAsync(sliceLength, token);
                object value = null;

                switch (property.Attribute.AttributeData)
                {
                    case TcpPackageDataType.MetaData:
                        value = _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpPackageDataType.Body:
                        value = property.PropertyType == typeof(byte[])
                            ? property.Attribute.Reverse ? _bitConverterHelper.Reverse(bytesFromReader) : bytesFromReader
                            : _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpPackageDataType.Id:
                        value = _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        tcpPackageId = value;
                        break;
                    case TcpPackageDataType.BodyLength:
                        value = _bitConverterHelper.ConvertFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        tcpPackageBodyLength = Convert.ToInt32(value);
                        break;
                }

                if (property.IsValueType)
                    response = (TResponse) property.SetInValueType(response, value);
                else
                    property.SetInClass(response, value);

                key += sliceLength;
                examined++;
                //Debug.WriteLine($"Deserialize property {sliceLength.ToString()} bytes, {property.Attribute.AttributeData.ToString()}:{property.Attribute.Index.ToString()}:{property.Attribute.Length.ToString()}");
            }

            if (examined != properties.Count)
                throw TcpPackageException.Throw(TcpPackageTypeException.SerializerSequenceViolated);

            return (tcpPackageId, tcpPackageBodyLength, response);
        }
    }
}