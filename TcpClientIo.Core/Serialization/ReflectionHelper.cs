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
        public TcpProperty LengthProperty { get; }
        public TcpProperty ComposeProperty { get; }

        public ReflectionHelper(Type dataType, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper, Type idType = null)
        {
            EnsureTypeHasRequiredAttributes(dataType);
            Properties = GetTypeProperties(dataType, byteArrayFactory, bitConverterHelper, idType);
            MetaLength = Properties.Sum(p => p.Attribute.Length);
            LengthProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Length);
            BodyProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Body);
            ComposeProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Compose);
        }

        private static List<TcpProperty> GetTypeProperties(Type dataType, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper, Type idType = null)
        {
            var tcpProperties = new List<TcpProperty>();

            foreach (var property in dataType.GetProperties())
            {
                var attribute = GetTcpDataAttribute(property);

                if (attribute == null)
                    continue;

                TcpProperty tcpProperty;
                if (attribute.TcpDataType == TcpDataType.Compose)
                {
                    var tcpComposition = new TcpComposition(property.PropertyType, byteArrayFactory, bitConverterHelper, idType);
                    tcpProperty = new TcpProperty(property, attribute, dataType, tcpComposition);
                }
                else
                    tcpProperty = new TcpProperty(property, attribute, dataType);

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

            var compose = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Compose).ToList();

            if (compose.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Compose));

            if (body.Count == 1 && compose.Count == 1)
                throw TcpException.AttributeBodyAndComposeViolated(type.ToString());

            var length = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Length).ToList();

            if (length.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Length));

            if (body.Count == 1)
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (length.Count == 0)
                    throw TcpException.AttributeLengthRequired(type.ToString(), nameof(TcpDataType.Body));

                if (length.Count == 1 && !CanReadWrite(length.Single()))
                    throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Length));
            }
            else if (compose.Count == 1)
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (length.Count == 0)
                    throw TcpException.AttributeLengthRequired(type.ToString(), nameof(TcpDataType.Compose));

                if (length.Count == 1 && !CanReadWrite(length.Single()))
                    throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Length));
            }
            else if (length.Count == 1)
                throw TcpException.AttributeRequiredWithLength(type.ToString());
            
            var metaData = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.MetaData).ToList();

            if (key.Count == 0 && length.Count == 0 && body.Count == 0 && metaData.Count == 0)
                throw TcpException.AttributesRequired(type.ToString());

            foreach (var item in metaData.Where(item => !CanReadWrite(item)))
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.MetaData), GetTcpDataAttribute(item).Index.ToString());

            static bool CanReadWrite(PropertyInfo property) => property.CanRead && property.CanWrite;
        }

        private static TcpDataAttribute GetTcpDataAttribute(ICustomAttributeProvider property)
            => property
                .GetCustomAttributes(true)
                .OfType<TcpDataAttribute>()
                .SingleOrDefault();
    }
}