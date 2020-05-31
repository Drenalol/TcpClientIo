using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Drenalol.Attributes;
using Drenalol.Exceptions;
using Microsoft.Extensions.Logging;

namespace Drenalol.Helpers
{
    internal class ReflectionHelper<TRequest, TResponse>
    {
        private readonly ILogger _logger;
        private readonly Type _request;
        private readonly Type _response;
        private ImmutableDictionary<Type, ImmutableDictionary<int, TcpProperty>> _internalCache = ImmutableDictionary<Type, ImmutableDictionary<int, TcpProperty>>.Empty;

        public ReflectionHelper(ILogger logger)
        {
            _logger = logger;
            _request = typeof(TRequest);
            _response = typeof(TResponse);
            GetAttributeData();
        }

        private void GetAttributeData()
        {
            var requestProperties = GetTypeProperties(_request);
            EnsureTypeHasRequiredAttributes(_request, requestProperties);
            ImmutableInterlocked.TryAdd(ref _internalCache, _request, requestProperties);

            if (_request != _response)
            {
                var responseProperties = GetTypeProperties(_response);
                EnsureTypeHasRequiredAttributes(_request, responseProperties);
                ImmutableInterlocked.TryAdd(ref _internalCache, _response, responseProperties);
            }
            else
                ImmutableInterlocked.TryAdd(ref _internalCache, _response, requestProperties);
        }

        private static ImmutableDictionary<int, TcpProperty> GetTypeProperties(Type type)
        {
            var tcpProperties =
                (from property in type.GetProperties()
                    let attribute = GetTcpDataAttribute(property)
                    where attribute != null
                    select new TcpProperty(property, attribute, type.IsValueType))
                .ToImmutableDictionary(key => key.Attribute.Index, property => property);

            TcpDataAttribute GetTcpDataAttribute(ICustomAttributeProvider property)
            {
                return property
                    .GetCustomAttributes(true)
                    .OfType<TcpDataAttribute>()
                    .SingleOrDefault();
            }

            return tcpProperties;
        }
        
        private void EnsureTypeHasRequiredAttributes(Type type, ImmutableDictionary<int, TcpProperty> properties)
        {
            var key = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.Id).ToList();

            if (key.Count > 1)
                throw TcpException.Throw(TcpTypeException.AttributeDuplicate, _logger, type.ToString(), nameof(TcpDataType.Id));

            if (key.Count == 1 && !key.Single().Value.CanReadWrite)
                throw TcpException.Throw(TcpTypeException.PropertyCanReadWrite, _logger, type.ToString(), nameof(TcpDataType.Id));

            var body = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.Body).ToList();

            if (body.Count > 1)
                throw TcpException.Throw(TcpTypeException.AttributeDuplicate, _logger, type.ToString(), nameof(TcpDataType.Body));
            
            if (body.Count == 1 && !body.Single().Value.CanReadWrite)
                throw TcpException.Throw(TcpTypeException.PropertyCanReadWrite, _logger, type.ToString(), nameof(TcpDataType.Body));

            var bodyLength = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.BodyLength).ToList();

            if (bodyLength.Count > 1)
                throw TcpException.Throw(TcpTypeException.AttributeDuplicate, _logger, type.ToString(), nameof(TcpDataType.BodyLength));
            if (body.Count == 1 && bodyLength.Count == 0)
                throw TcpException.Throw(TcpTypeException.AttributeBodyLengthRequired, _logger, type.ToString());
            if (bodyLength.Count == 1 && body.Count == 0)
                throw TcpException.Throw(TcpTypeException.AttributeBodyRequired, _logger, type.ToString());
            
            if (bodyLength.Count == 1 && body.Count == 1 && !bodyLength.Single().Value.CanReadWrite)
                throw TcpException.Throw(TcpTypeException.PropertyCanReadWrite, _logger, type.ToString(), nameof(TcpDataType.BodyLength));
            
            var metaData = properties.Where(item => item.Value.Attribute.TcpDataType == TcpDataType.MetaData).ToList();
            
            if (key.Count == 0 && bodyLength.Count == 0 && body.Count == 0 && metaData.Count == 0)
                throw TcpException.Throw(TcpTypeException.AttributesRequired, _logger, type.ToString());

            foreach (var pair in metaData.Where(pair => !pair.Value.CanReadWrite))
            {
                throw TcpException.Throw(TcpTypeException.PropertyCanReadWrite, _logger, type.ToString(), nameof(TcpDataType.MetaData), pair.Value.Attribute.Index.ToString());
            }
        }

        public ImmutableDictionary<int, TcpProperty> GetRequestProperties() => _internalCache[_request];
        
        public ImmutableDictionary<int, TcpProperty> GetResponseProperties() => _internalCache[_response];
    }
}