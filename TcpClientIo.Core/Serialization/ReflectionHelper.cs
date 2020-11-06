using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class ReflectionHelper<TRequest, TResponse>
    {
        private readonly Type _request;
        private readonly Type _response;

        public IReadOnlyDictionary<int, TcpProperty> RequestProperties { get; private set; }
        public IReadOnlyDictionary<int, TcpProperty> ResponseProperties { get; private set; }
        public int RequestMetaLength { get; private set; }
        public int ResponseMetaLength { get; private set; }
        public TcpProperty ResponseBodyLengthProperty { get; private set; }

        public ReflectionHelper()
        {
            _request = typeof(TRequest);
            _response = typeof(TResponse);
            GetAttributeData();
        }

        private void GetAttributeData()
        {
            var requestProperties = GetTypeProperties(_request);
            EnsureTypeHasRequiredAttributes(_request, requestProperties);
            RequestProperties = requestProperties;

            if (_request != _response)
            {
                var responseProperties = GetTypeProperties(_response);
                EnsureTypeHasRequiredAttributes(_request, responseProperties);
                ResponseProperties = responseProperties;
            }
            else
                ResponseProperties = requestProperties;

            RequestMetaLength = RequestProperties.Values.Sum(p => p.Attribute.Length);
            ResponseMetaLength = ResponseProperties.Values.Sum(p => p.Attribute.Length);
            ResponseBodyLengthProperty = ResponseProperties.SingleOrDefault(p => p.Value.Attribute.TcpDataType == TcpDataType.BodyLength).Value;
        }

        private static Dictionary<int, TcpProperty> GetTypeProperties(Type type)
        {
            var tcpProperties =
                (from property in type.GetProperties()
                    let attribute = GetTcpDataAttribute(property)
                    where attribute != null
                    select new TcpProperty(property, attribute, type))
                .ToDictionary(key => key.Attribute.Index, property => property);

            TcpDataAttribute GetTcpDataAttribute(ICustomAttributeProvider property)
            {
                return property
                    .GetCustomAttributes(true)
                    .OfType<TcpDataAttribute>()
                    .SingleOrDefault();
            }

            return tcpProperties;
        }

        private static void EnsureTypeHasRequiredAttributes(Type type, Dictionary<int, TcpProperty> properties)
        {
            var key = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.Id).ToList();

            if (key.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Id));

            if (key.Count == 1 && !key.Single().Value.CanReadWrite)
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Id));

            var body = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.Body).ToList();

            if (body.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Body));

            if (body.Count == 1 && !body.Single().Value.CanReadWrite)
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Body));

            var bodyLength = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.BodyLength).ToList();

            if (bodyLength.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.BodyLength));
            
            if (body.Count == 1 && bodyLength.Count == 0)
                throw TcpException.AttributeBodyLengthRequired(type.ToString());
            
            if (bodyLength.Count == 1 && body.Count == 0)
                throw TcpException.AttributeBodyRequired(type.ToString());

            if (bodyLength.Count == 1 && body.Count == 1 && !bodyLength.Single().Value.CanReadWrite)
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.BodyLength));

            var metaData = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.MetaData).ToList();

            if (key.Count == 0 && bodyLength.Count == 0 && body.Count == 0 && metaData.Count == 0)
                throw TcpException.AttributesRequired(type.ToString());

            foreach (var pair in metaData.Where(pair => !pair.Value.CanReadWrite))
            {
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.MetaData), pair.Value.Attribute.Index.ToString());
            }
        }
    }
}