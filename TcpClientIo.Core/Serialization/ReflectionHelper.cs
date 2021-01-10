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
        public TcpProperty BodyLengthProperty { get; }

        public ReflectionHelper(Type model, Func<int, byte[]> byteArrayFactory, BitConverterHelper bitConverterHelper)
        {
            Properties = GetTypeProperties(model, byteArrayFactory, bitConverterHelper);
            EnsureTypeHasRequiredAttributes(model, Properties);
            MetaLength = Properties.Sum(p => p.Attribute.Length);
            BodyLengthProperty = Properties.SingleOrDefault(p => p.Attribute.TcpDataType == TcpDataType.BodyLength);
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

            static TcpDataAttribute GetTcpDataAttribute(ICustomAttributeProvider property)
            {
                return property
                    .GetCustomAttributes(true)
                    .OfType<TcpDataAttribute>()
                    .SingleOrDefault();
            }

            return tcpProperties;
        }

        private static void EnsureTypeHasRequiredAttributes(Type type, IReadOnlyList<TcpProperty> properties)
        {
            var key = properties.Where(item => item.Attribute.TcpDataType == TcpDataType.Id).ToList();

            if (key.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Id));

            if (key.Count == 1 && !key.Single().CanReadWrite)
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Id));

            var body = properties.Where(item => item.Attribute.TcpDataType == TcpDataType.Body).ToList();

            if (body.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.Body));

            if (body.Count == 1 && !body.Single().CanReadWrite)
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.Body));

            var bodyLength = properties.Where(item => item.Attribute.TcpDataType == TcpDataType.BodyLength).ToList();

            if (bodyLength.Count > 1)
                throw TcpException.AttributeDuplicate(type.ToString(), nameof(TcpDataType.BodyLength));

            if (body.Count == 1 && bodyLength.Count == 0)
                throw TcpException.AttributeBodyLengthRequired(type.ToString());

            if (bodyLength.Count == 1 && body.Count == 0)
                throw TcpException.AttributeBodyRequired(type.ToString());

            if (bodyLength.Count == 1 && body.Count == 1 && !bodyLength.Single().CanReadWrite)
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.BodyLength));

            var metaData = properties.Where(item => item.Attribute.TcpDataType == TcpDataType.MetaData).ToList();

            if (key.Count == 0 && bodyLength.Count == 0 && body.Count == 0 && metaData.Count == 0)
                throw TcpException.AttributesRequired(type.ToString());

            foreach (var pair in metaData.Where(pair => !pair.CanReadWrite))
            {
                throw TcpException.PropertyCanReadWrite(type.ToString(), nameof(TcpDataType.MetaData), pair.Attribute.Index.ToString());
            }
        }
    }
}