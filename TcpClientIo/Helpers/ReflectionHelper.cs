using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Drenalol.Attributes;
using Drenalol.Exceptions;

namespace Drenalol.Helpers
{
    internal class ReflectionHelper<TRequest, TResponse>
    {
        private readonly Type _request;
        private readonly Type _response;
        private ImmutableDictionary<Type, ImmutableDictionary<int, TcpPackageProperty>> _internalCache = ImmutableDictionary<Type, ImmutableDictionary<int, TcpPackageProperty>>.Empty;

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

        private static ImmutableDictionary<int, TcpPackageProperty> GetTypeProperties(Type type)
        {
            var tcpPackageProperties =
                (from property in type.GetProperties()
                    let attribute = GetTcpPackageDataAttribute(property)
                    where attribute != null
                    select new TcpPackageProperty(property, attribute, type.IsValueType))
                .ToImmutableDictionary(key => key.Attribute.Index, property => property);

            static TcpPackageDataAttribute GetTcpPackageDataAttribute(ICustomAttributeProvider property)
            {
                return property
                    .GetCustomAttributes(true)
                    .OfType<TcpPackageDataAttribute>()
                    .SingleOrDefault();
            }

            return tcpPackageProperties;
        }
        
        private static void EnsureTypeHasRequiredAttributes(Type type, ImmutableDictionary<int, TcpPackageProperty> properties)
        {
            var key = properties.Where(item => item.Value.Attribute.AttributeData == TcpPackageDataType.Id).ToList();

            if (key.Count == 0)
                throw TcpPackageException.Throw(TcpPackageTypeException.AttributeKeyRequired, type.ToString());
            if (key.Count > 1)
                throw TcpPackageException.Throw(TcpPackageTypeException.AttributeDuplicate, type.ToString(), nameof(TcpPackageDataType.Id));

            if (!key.Single().Value.CanReadWrite)
                throw TcpPackageException.Throw(TcpPackageTypeException.PropertyCanReadWrite, type.ToString(), nameof(TcpPackageDataType.Id));

            var body = properties.Where(item => item.Value.Attribute.AttributeData == TcpPackageDataType.Body).ToList();

            if (body.Count > 1)
                throw TcpPackageException.Throw(TcpPackageTypeException.AttributeDuplicate, type.ToString(), nameof(TcpPackageDataType.Body));
            
            if (body.Count == 1 && !body.Single().Value.CanReadWrite)
                throw TcpPackageException.Throw(TcpPackageTypeException.PropertyCanReadWrite, type.ToString(), nameof(TcpPackageDataType.Body));

            var bodyLength = properties.Where(item => item.Value.Attribute.AttributeData == TcpPackageDataType.BodyLength).ToList();

            if (bodyLength.Count > 1)
                throw TcpPackageException.Throw(TcpPackageTypeException.AttributeDuplicate, type.ToString(), nameof(TcpPackageDataType.BodyLength));
            if (body.Count == 1 && bodyLength.Count == 0)
                throw TcpPackageException.Throw(TcpPackageTypeException.AttributeBodyLengthRequired, type.ToString());
            if (bodyLength.Count == 1 && body.Count == 0)
                throw TcpPackageException.Throw(TcpPackageTypeException.AttributeBodyRequired, type.ToString());
            
            if (bodyLength.Count == 1 && body.Count == 1 && !bodyLength.Single().Value.CanReadWrite)
                throw TcpPackageException.Throw(TcpPackageTypeException.PropertyCanReadWrite, type.ToString(), nameof(TcpPackageDataType.BodyLength));
        }

        public ImmutableDictionary<int, TcpPackageProperty> GetRequestProperties() => _internalCache[_request];
        
        public ImmutableDictionary<int, TcpPackageProperty> GetResponseProperties() => _internalCache[_response];
    }
}