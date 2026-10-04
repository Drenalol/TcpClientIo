using System.Buffers;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Extensions;
using Drenalol.TcpClientIo.Serialization.Pipelines;

namespace Drenalol.TcpClientIo.Serialization;

internal class TcpDeserializer<TId, TData>(BitConverterHelper bitConverterHelper, PipeReaderExecutor pipeReaderExecutor) where TId : struct where TData : new()
{
    private readonly ReflectionHelper _reflection = new(typeof(TData));

    public async Task<(TId, TData)> DeserializePipeAsync(CancellationToken token)
    {
        TData data;
        TId id;

        var metaReadResult = await pipeReaderExecutor.ReadLengthAsync(_reflection.MetaLength, token);

        if (_reflection.LengthProperty == null)
        {
            var sequence = metaReadResult.Slice(_reflection.MetaLength);
            (id, data) = Deserialize(sequence);
            pipeReaderExecutor.Consume(sequence.GetPosition(_reflection.MetaLength));
        }
        else
        {
            var lengthAttribute = _reflection.LengthProperty.Attribute;
            var lengthSequence = metaReadResult.Slice(lengthAttribute.Length, lengthAttribute.Index);
            var lengthValue = bitConverterHelper.ConvertFromSequence(lengthSequence, _reflection.LengthProperty.PropertyType, lengthAttribute.Reverse);
            var totalLength = _reflection.MetaLength + (lengthValue is int length ? length : Convert.ToInt32(lengthValue));

            ReadOnlySequence<byte> sequence;

            if (metaReadResult.Buffer.Length >= totalLength)
                sequence = metaReadResult.Slice(totalLength);
            else
            {
                pipeReaderExecutor.Examine(metaReadResult.Buffer.Start, metaReadResult.Buffer.GetPosition(_reflection.MetaLength));
                var totalReadResult = await pipeReaderExecutor.ReadLengthAsync(totalLength, token);
                sequence = totalReadResult.Slice(totalLength);
            }

            (id, data) = Deserialize(sequence, lengthValue);
            pipeReaderExecutor.Consume(sequence.GetPosition(totalLength));
        }

        return (id, data);
    }

    public (TId, TData) Deserialize(in ReadOnlySequence<byte> sequence, object? preKnownLength = null)
    {
        var data = new TData();
        TId id = default;

        var length = 0;
        var propertyIndex = 0;

        foreach (var property in _reflection.Properties)
        {
            object value;
            int sliceLength;

            if (property.Attribute.TcpDataType == TcpDataType.Length && preKnownLength != null)
            {
                value = preKnownLength;
                length = preKnownLength is int lengthValue ? lengthValue : Convert.ToInt32(preKnownLength);
                sliceLength = property.Attribute.Length;
                SetValue();
                continue;
            }

            sliceLength = property.Attribute.TcpDataType switch
            {
                TcpDataType.MetaData or TcpDataType.Id or TcpDataType.Length => property.Attribute.Length,
                TcpDataType.Body => length,
                _ => throw new ArgumentOutOfRangeException()
            };

            var slice = sequence.Slice(propertyIndex, sliceLength);

            value = bitConverterHelper.ConvertFromSequence(slice, property.PropertyType, property.Attribute.Reverse);

            if (property.Attribute.TcpDataType == TcpDataType.Id)
                id = (TId)value;
            else if (property.Attribute.TcpDataType == TcpDataType.Length)
                length = value is int lengthValue ? lengthValue : Convert.ToInt32(value);

            SetValue();

            void SetValue()
            {
                if (property.IsValueType)
                    data = (TData)property.Set(data, value);
                else
                    property.Set(data, value);

                propertyIndex += sliceLength;
            }
        }

        return (id, data);
    }
}
