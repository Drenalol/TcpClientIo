using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization
{
    public class TcpSerializer<TId, TRequest, TResponse> where TResponse : new() where TId : struct
    {
        private readonly Func<int, byte[]> _byteArrayFactory;
        private readonly ReflectionHelper<TRequest, TResponse> _reflectionHelper;
        private readonly BitConverterHelper _bitConverterHelper;

        public TcpSerializer(IReadOnlyCollection<TcpConverter> converters, Func<int, byte[]> byteArrayFactory)
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
                    realLength = (int) lengthValue + _reflectionHelper.RequestMetaCount;
                }
                catch (InvalidCastException)
                {
                    realLength = Convert.ToInt32(lengthValue) + _reflectionHelper.RequestMetaCount;
                }
            }
            else
                realLength = _reflectionHelper.RequestMetaCount;

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
            var response = new TResponse();
            var key = 0;
            var examined = 0;
            TId id = default;
            var bodyLength = 0;
            var properties = _reflectionHelper.ResponseProperties;

            while (properties.TryGetValue(key, out var property))
            {
                ReadResult readResult;
                var isBody = property.Attribute.TcpDataType == TcpDataType.Body;
                var sliceLength = isBody ? bodyLength : property.Attribute.Length;
                var isEmptyBody = isBody && sliceLength == 0;

                if (isEmptyBody)
                    readResult = new ReadResult(ReadOnlySequence<byte>.Empty, false, false);
                else
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        readResult = await pipeReader.ReadAsync(token);

                        if (readResult.IsCanceled)
                            throw new OperationCanceledException();

                        if (readResult.IsCompleted)
                            throw new EndOfStreamException();

                        if (readResult.Buffer.IsEmpty)
                            continue;

                        var readResultLength = readResult.Buffer.Length;

                        if (readResultLength < sliceLength)
                        {
                            pipeReader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.GetPosition(readResultLength));
                            continue;
                        }

                        break;
                    }

                var slice = readResult.Buffer.Slice(0, sliceLength);
                var value = _bitConverterHelper.ConvertFromBytes(slice, property.PropertyType, property.Attribute.Reverse);

                switch (property.Attribute.TcpDataType)
                {
                    case TcpDataType.Id:
                        id = (TId) value;
                        break;
                    case TcpDataType.BodyLength:
                        bodyLength = Convert.ToInt32(value);
                        break;
                }

                if (!isEmptyBody)
                    pipeReader.AdvanceTo(readResult.Buffer.GetPosition(sliceLength));

                if (property.IsValueType)
                    response = (TResponse) property.Set(response, value);
                else
                    property.Set(response, value);

                key += sliceLength;
                examined++;

                if (examined == properties.Count)
                    break;
            }

            return (id, response);
        }
    }
}