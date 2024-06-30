using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreshPager.Data.Marshal;

public class StringToOptionalIntConverter: JsonConverter<int?> {

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return int.TryParse(reader.GetString(), out int number) ? number : null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options) => throw new NotImplementedException();

}