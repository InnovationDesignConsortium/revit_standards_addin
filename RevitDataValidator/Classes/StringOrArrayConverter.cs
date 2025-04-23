using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class StringOrArrayConverter : JsonConverter<List<string>>
    {
        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.String)
                return new List<string> { token.ToString() };
            if (token.Type == JTokenType.Array)
                return token.ToObject<List<string>>();
            return null;
        }

        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
        {
            if (value.Count == 1)
                writer.WriteValue(value[0]);
            else
                serializer.Serialize(writer, value);
        }
    }
}