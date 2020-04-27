using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TcpClientDuplex.Sub;

namespace TcpClientDuplex.Extensions
{
    public static class JsonExt
    {
        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> {new JsonConverterIpAddress()}
        };

        public static string Serialize(object obj) => JsonConvert.SerializeObject(obj, JsonSerializerSettings);

        public static T Deserialize<T>(string s) => JsonConvert.DeserializeObject<T>(s, JsonSerializerSettings);
    }
}