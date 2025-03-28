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
            var val = reader.GetString()!;
            var hexString = reader.GetString();
            if (hexString.TryParseHex<uint>(out var result))
                return result;

            throw (new Exception());
        }

        public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("X8"));
        }
    }
}
