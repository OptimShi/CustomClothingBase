using System.Buffers.Text;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization.Metadata;

namespace CustomClothingBase;

public class HexConverter<T> : JsonConverter<T> where T : INumber<T>
{
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue($"0x{value:X}");

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
//        if ((reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Number))
        if ((reader.TokenType == JsonTokenType.String))
        {
            var hexString = reader.GetString();
            //if (reader.GetString() is string hexString)
                if (hexString.TryParseHex<T>(out var result))
                    return result;

        }
        else if(reader.TokenType == JsonTokenType.Number)
        {
            var intString = reader.GetUInt32().ToString();
            if (intString.TryParseHex<T>(out var result))
                return result;
        }

        throw new JsonException("Invalid format for hex number");
    }
}



/// <summary>
/// Generic Dictionary converted with numeric keys
/// </summary>
public class HexKeyDictionaryConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>> where TKey : struct, IComparable, IFormattable, INumber<TKey>
{
    public override Dictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token.");

        var dictionary = new Dictionary<TKey, TValue>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token.");

            string propertyName = reader.GetString();
            // Check if the key is a hexadecimal string
            if (propertyName != null && propertyName.TryParseHex<TKey>(out var result))
            {
                // Read the value
                reader.Read();
                TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                dictionary[result] = value;
            }
            else
            {
                throw new JsonException("Invalid key format. Expected hexadecimal string starting with '0x'.");
            }

        }

        throw new JsonException("Unexpected end of JSON while reading dictionary.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            // Write the key as a hex string
            string keyAsHex = ConvertToHexString(kvp.Key);
            writer.WritePropertyName(keyAsHex);

            // Serialize the value
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private string ConvertToHexString(TKey key)
    {
        if (key is IFormattable formattableKey)
            return $"0x{formattableKey.ToString("X", null)}";

        throw new JsonException("Key type is not a valid numeric type.");
    }
}

public static class HexExtensions
{
    public static bool TryParseHex<T>(this string hexString, out T result) where T : INumber<T>
    {
        if (hexString.StartsWith("0x"))
            return T.TryParse(hexString.Substring(2), NumberStyles.HexNumber, null, out result);
        else
            return T.TryParse(hexString, NumberStyles.Number, null, out result);

    }
}

public class HexTypeResolver(HashSet<string> Keys) : DefaultJsonTypeInfoResolver
{
    public HashSet<string> Keys { get; set; } = Keys;
    HexConverter<uint> uintConverter = new();
    HexConverter<int> intConverter = new();

    public override JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        // Apply the converter only to the target type
        if (jsonTypeInfo != null) //&& type == typeof(ClothingTable))
        {
            foreach (var property in jsonTypeInfo.Properties)
            {
                //Base it off names?
                var name = property.Name;
                if (!Keys.Contains(name))
                    continue;

                var pType = property.PropertyType;
                if (property.PropertyType == typeof(int))
                    property.CustomConverter = intConverter;
                if (property.PropertyType == typeof(uint))
                    property.CustomConverter = uintConverter;
            }
        }

        return jsonTypeInfo;
    }
}