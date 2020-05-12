using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Drenalol.Attributes;
using Drenalol.Exceptions;
using Drenalol.Extensions;
using Drenalol.Helpers;
using static Drenalol.Helpers.BitConverterHelper;

namespace Drenalol.Base
{
    public class TcpPackageSerializer<TRequest, TResponse> where TResponse : new()
    {
        private readonly ReflectionHelper<TRequest, TResponse> _reflectionHelper;
        
        public TcpPackageSerializer()
        {
            _reflectionHelper = new ReflectionHelper<TRequest, TResponse>();
        }
        
        public byte[] Serialize(TRequest request)
        {
            byte[] serializedRequest = null;
            var key = 0;
            var examined = 0;

            var properties = _reflectionHelper.GetRequestProperties();

            while (properties.TryGetValue(key, out var property))
            {
                var value = CustomBitConverterToBytes(property.Get(request), property.PropertyType, property.Attribute.Reverse);
                var valueLength = value.Length;
                Debug.WriteLine($"Serialize property {valueLength.ToString()} bytes, {property.Attribute.AttributeData.ToString()}:{property.Attribute.Index.ToString()}:{property.Attribute.Length.ToString()}");

                if (property.Attribute.AttributeData != TcpPackageDataType.Body && valueLength > property.Attribute.Length)
                    throw TcpPackageException.Throw(TcpPackageTypeException.SerializerLengthOutOfRange, property.PropertyType.ToString(), valueLength.ToString(), property.Attribute.Length.ToString());

                var length = property.Attribute.AttributeData == TcpPackageDataType.Body ? valueLength : property.Attribute.Length;
                var rent = new byte[length];
                Array.Copy(value, rent, valueLength);
                Array.Resize(ref serializedRequest, property.Attribute.Index + length);
                Array.Copy(rent, 0, serializedRequest, property.Attribute.Index, length);
                key += length;
                examined++;
            }

            if (examined != properties.Count)
                throw TcpPackageException.Throw(TcpPackageTypeException.SerializerSequenceViolated);

            return serializedRequest;
        }
        
        public async Task<(object, TResponse)> DeserializeAsync(PipeReader pipeReader, CancellationToken token)
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
                    case TcpPackageDataType.Key:
                    case TcpPackageDataType.BodyLength:
                    case TcpPackageDataType.MetaData:
                        sliceLength = property.Attribute.Length;
                        break;
                    case TcpPackageDataType.Body:
                        sliceLength = tcpPackageBodyLength;
                        break;
                }

                Debug.WriteLine($"Deserialize property {sliceLength.ToString()} bytes, {property.Attribute.AttributeData.ToString()}:{property.Attribute.Index.ToString()}:{property.Attribute.Length.ToString()}");
                var bytesFromReader = await pipeReader.ReadLengthAsync(sliceLength, token);
                object value = null;

                switch (property.Attribute.AttributeData)
                {
                    case TcpPackageDataType.MetaData:
                        value = CustomBitConverterFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        break;
                    case TcpPackageDataType.Body:
                        value = property.Attribute.Reverse ? Reverse(bytesFromReader) : bytesFromReader;
                        break;
                    case TcpPackageDataType.Key:
                        value = CustomBitConverterFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        tcpPackageId = value;
                        break;
                    case TcpPackageDataType.BodyLength:
                        value = CustomBitConverterFromBytes(bytesFromReader, property.PropertyType, property.Attribute.Reverse);
                        tcpPackageBodyLength = Convert.ToInt32(value);
                        break;
                }

                property.Set(response, value);
                key += sliceLength;
                examined++;
            }

            if (examined != properties.Count)
                throw TcpPackageException.Throw(TcpPackageTypeException.SerializerSequenceViolated);

            return (tcpPackageId, response);
        }
    }
}