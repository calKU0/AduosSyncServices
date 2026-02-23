using System.Text.Json;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Infrastructure.Helpers
{
    public static class Converters
    {
        public class EmptyStringToListConverter<T> : JsonConverter<List<T>>
        {
            public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        var s = reader.GetString();
                        if (string.IsNullOrWhiteSpace(s))
                            return new List<T>();
                        throw new JsonException($"Unexpected string value for list: {s}");

                    case JsonTokenType.StartArray:
                        return JsonSerializer.Deserialize<List<T>>(ref reader, options);

                    case JsonTokenType.Null:
                        return new List<T>();

                    default:
                        throw new JsonException($"Unexpected token {reader.TokenType} for list");
                }
            }

            public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}