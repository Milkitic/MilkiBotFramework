using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Internal
{
    internal class UnixDateTimeConverter : JsonConverter<DateTimeOffset>
    {
        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Type ObjectType = typeof(DateTimeOffset);

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {

            long result;
            if (reader.TokenType == JsonTokenType.Number)
            {
                result = reader.GetInt64();
            }
            else
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException(
                        $"Unexpected token parsing date. Expected Integer or String, got {reader.TokenType}.");

                var str = reader.GetString();
                if (!long.TryParse(str, out result))
                    throw new JsonException($"Cannot convert invalid value to {ObjectType}.");
            }

            DateTime dateTime = result >= 0L
                ? UnixEpoch.AddSeconds(result)
                : throw new JsonException(
                    $"Cannot convert value that is before Unix epoch of 00:00:00 UTC on 1 January 1970 to {ObjectType}.");
            return new DateTimeOffset(dateTime, TimeSpan.Zero);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            var totalSeconds = (long)(value.ToUniversalTime() - UnixEpoch).TotalSeconds;

            if (totalSeconds < 0L)
                throw new JsonException("Cannot convert date value that is before Unix epoch of 00:00:00 UTC on 1 January 1970.");
            writer.WriteNumberValue(totalSeconds);
        }

        private static bool IsNullable(Type t)
        {
            return !t.IsValueType || IsNullableType(t);
        }

        private static bool IsNullableType(Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
