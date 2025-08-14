using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomClothingBase.JsonConverters
{
    public class HexUintJsonConverter : JsonConverter<uint>
    {
        public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string hexString;
            string intString;
            if (reader.TokenType == JsonTokenType.String)
            {
                hexString = reader.GetString();
                if(hexString == "0")
                    return 0;

                if (hexString is not null && hexString.TryParseHex<uint>(out var result))
                    return result;

            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                intString = reader.GetUInt32().ToString();
                if (intString.TryParseHex<uint>(out var result))
                    return result;
            }

            throw new JsonException("Invalid format for hex number");
        }

        public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
        {
            writer.WriteStringValue("0x" + value.ToString("X8"));
        }
    }

    public class HexKeyUintJsonConverter<TValue> : JsonConverter<Dictionary<uint, TValue>>
    {
        public override Dictionary<uint, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new Dictionary<uint, TValue>();

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token");

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return result;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected PropertyName token");

                string propertyName = reader.GetString();

                if (!propertyName.TryParseHex<uint>(out var key))
                {
                    throw new JsonException($"Invalid key format: '{propertyName}' is not an integer or hex value");
                }


                reader.Read();
                TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                result[key] = value;
            }

            throw new JsonException("Unexpected end of JSON");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<uint, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                writer.WritePropertyName("0x" + kvp.Key.ToString("X8"));
                JsonSerializer.Serialize(writer, kvp.Value, options);
            }
            writer.WriteEndObject();
        }
    }
}
