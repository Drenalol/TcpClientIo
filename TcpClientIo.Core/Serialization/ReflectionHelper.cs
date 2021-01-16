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

        public ReflectionHelper(Type typeData, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper, Type typeId = null)
        {
            EnsureTypeHasRequiredAttributes(typeData);
            Properties = GetTypeProperties(typeData, byteArrayFactory, bitConverterHelper, typeId);
            MetaLength = Properties.Sum(p => p.Attribute.Length);
            LengthProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Length);
            BodyProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Body);
            ComposeProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.Compose);
        }

        private static List<TcpProperty> GetTypeProperties(Type typeData, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper, Type typeId = null)
        {
            var tcpProperties = new List<TcpProperty>();

            foreach (var property in typeData.GetProperties())
            {
                var attribute = GetTcpDataAttribute(property);

                if (attribute == null)
                    continue;

                TcpProperty tcpProperty;
                if (attribute.TcpDataType == TcpDataType.Compose && !property.PropertyType.IsPrimitive)
                {
                    var tcpComposition = new TcpComposition(property.PropertyType, byteArrayFactory, bitConverterHelper, typeId);
                    tcpProperty = new TcpProperty(property, attribute, typeData, tcpComposition);
                }
                else
                    tcpProperty = new TcpProperty(property, attribute, typeData);

                tcpProperties.Add(tcpProperty);
            }

            return tcpProperties;
        }

        private static void EnsureTypeHasRequiredAttributes(Type typeData)
        {
            var properties = typeData.GetProperties();

            var key = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Id).ToList();

            if (key.Count > 1)
                throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Id));

            if (key.Count == 1 && !CanReadWrite(key.Single()))
                throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Id));

            var body = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Body).ToList();

            if (body.Count > 1)
                throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Body));

            if (body.Count == 1 && !CanReadWrite(body.Single()))
                throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Body));

            var compose = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Compose).ToList();

            if (compose.Count > 1)
                throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Compose));

            if (body.Count == 1 && compose.Count == 1)
                throw TcpException.AttributeBodyAndComposeViolated(typeData.ToString());

            var length = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.Length).ToList();

            if (length.Count > 1)
                throw TcpException.AttributeDuplicate(typeData.ToString(), nameof(TcpDataType.Length));

            if (body.Count == 1)
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (length.Count == 0)
                    throw TcpException.AttributeLengthRequired(typeData.ToString(), nameof(TcpDataType.Body));

                if (length.Count == 1 && !CanReadWrite(length.Single()))
                    throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Length));
            }
            else if (compose.Count == 1)
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (length.Count == 0)
                    throw TcpException.AttributeLengthRequired(typeData.ToString(), nameof(TcpDataType.Compose));

                if (length.Count == 1 && !CanReadWrite(length.Single()))
                    throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.Length));
            }
            else if (length.Count == 1)
                throw TcpException.AttributeRequiredWithLength(typeData.ToString());
            
            var metaData = properties.Where(item => GetTcpDataAttribute(item).TcpDataType == TcpDataType.MetaData).ToList();

            if (key.Count == 0 && length.Count == 0 && body.Count == 0 && metaData.Count == 0)
                throw TcpException.AttributesRequired(typeData.ToString());

            foreach (var item in metaData.Where(item => !CanReadWrite(item)))
                throw TcpException.PropertyCanReadWrite(typeData.ToString(), nameof(TcpDataType.MetaData), GetTcpDataAttribute(item).Index.ToString());

            static bool CanReadWrite(PropertyInfo property) => property.CanRead && property.CanWrite;
        }

        private static TcpDataAttribute GetTcpDataAttribute(ICustomAttributeProvider property)
            => property
                .GetCustomAttributes(true)
                .OfType<TcpDataAttribute>()
                .SingleOrDefault();
    }
}