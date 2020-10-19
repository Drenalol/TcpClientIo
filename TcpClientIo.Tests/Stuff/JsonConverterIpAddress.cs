using System;
using System.Net;
using Newtonsoft.Json;

namespace Drenalol.TcpClientIo.Stuff
{
    public class JsonConverterIpAddress : JsonConverter<IPAddress>
    {
        public override void WriteJson(JsonWriter writer, IPAddress value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override IPAddress ReadJson(JsonReader reader, Type objectType, IPAddress existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value != null && IPAddress.TryParse(reader.Value.ToString(), out var ip))
                return ip;
            
            return IPAddress.Any;
        }
    }
}