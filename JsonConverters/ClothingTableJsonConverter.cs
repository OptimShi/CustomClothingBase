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
            if (reader.TokenType == JsonTokenType.String)
            {
                var hexString = reader.GetString();
                if (hexString is not null && hexString.TryParseHex<uint>(out var result))
                    return result;

            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                var intString = reader.GetUInt32().ToString();
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
}
