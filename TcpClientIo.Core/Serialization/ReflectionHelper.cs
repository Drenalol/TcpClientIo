using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Exceptions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class ReflectionHelper
    {
        public IReadOnlyList<TcpProperty> Properties { get; }
        public int MetaLength { get; }
        public TcpProperty BodyProperty { get; }
        public TcpProperty BodyLengthProperty { get; }
        public TcpProperty ComposeProperty { get; }

        public ReflectionHelper(Type model, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper)
        {
            EnsureTypeHasRequiredAttributes(model);
            Properties = GetTypeProperties(model, byteArrayFactory, bitConverterHelper);
            MetaLength = Properties.Sum(p => p.Attribute.Length);
            BodyProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Body);
            BodyLengthProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.BodyLength);
            ComposeProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Compose);
        }

        private static List<TcpProperty> GetTypeProperties(Type type, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper)
        {
            var tcpProperties = new List<TcpProperty>();

            foreach (var property in type.GetProperties())
            {
                var attribute = GetTcpDataAttribute(property);

                if (attribute == null)
                    continue;

                TcpProperty tcpProperty;
                if (attribute.TcpDataType == TcpDataType.Compose)
                {
                    var tcpComposition = new TcpComposition(property.PropertyType, byteArrayFactory, bitConverterHelper);
                    tcpProperty = new TcpProperty(property, attribute, type, tcpComposition);
                }
                else
                    tcpProperty = new TcpProperty(property, attribute, type);

                tcpProperties.Add(tcpProperty);
            }

            return tcpProperties;
        }

        private static void EnsureTypeHasRequiredAttributes(Type type)
        {
            var properties = type.GetProperties();

            var key = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Id).ToList();

            if (key.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Id));

            if (key.Count == 1 && !CanReadWrite(key.Single()))
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Id));

            var body = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Body).ToList();

            if (body.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Body));

            if (body.Count == 1 && !CanReadWrite(body.Single()))
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Body));

            var bodyLength = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.BodyLength).ToList();

            if (bodyLength.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.BodyLength));

            if (body.Count == 1 && bodyLength.Count == 0)
                throw TcpException.AttributeBodyLengthRequired(type.ToString());

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (bodyLength.Count == 1 && body.Count == 0)
                throw TcpException.AttributeBodyRequired(type.ToString());

            if (bodyLength.Count == 1 && body.Count == 1 && !CanReadWrite(bodyLength.Single()))
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.BodyLength));

            var metaData = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.MetaData).ToList();

            if (key.Count == 0 && bodyLength.Count == 0 && body.Count == 0 && metaData.Count == 0)
                throw TcpException.AttributesRequired(type.ToString());

            foreach (var item in metaData.Where(item => !CanReadWrite(item)))
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.MetaData), GetTcpDataAttribute(item).Index.ToString());

            var compose = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Compose).ToList();

            if (compose.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Compose));

            if (body.Count == 1 && compose.Count == 1)
                throw TcpException.AttributeBodyAndComposeViolated(type.ToString());

            static bool CanReadWrite(PropertyInfo property) => property.CanRead && property.CanWrite;
        }

        private static TcpDataAttribute GetTcpDataAttribute(ICustomAttributeProvider property)
            => property
                .GetCustomAttributes(true)
                .OfType<TcpDataAttribute>()
                .SingleOrDefault();
    }
}