using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Internal
{
    internal class Int64ToStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();
            var l = reader.GetInt64();
            return l.ToString();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(long.Parse(value));
        }
    }
}
