using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreshPager.Data.Marshal;

public abstract class StringToTimespanConverter: JsonConverter<TimeSpan> {

    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } rawString ? intToTimeSpan(double.Parse(rawString)) : default;

    protected abstract TimeSpan intToTimeSpan(double number);

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) => throw new NotImplementedException();

    public class FromSeconds: StringToTimespanConverter {

        protected override TimeSpan intToTimeSpan(double number) => TimeSpan.FromSeconds(number);

    }

    public class FromMilliseconds: StringToTimespanConverter {

        protected override TimeSpan intToTimeSpan(double number) => TimeSpan.FromMilliseconds(number);

    }

}