using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreshPager.Data.Marshal;

public abstract class StringToTimespanConverter: JsonConverter<TimeSpan> {

    /// <exception cref="JsonException">A converter may throw any Exception, but should throw <see cref="JsonException"/> when the JSON is invalid.</exception>
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        try {
            return reader.GetString() is { } rawString ? intToTimeSpan(double.Parse(rawString)) : default;
        } catch (FormatException e) {
            throw new JsonException("Failed to parse JSON string as TimeSpan", e);
        } catch (OverflowException e) {
            throw new JsonException("Failed to parse JSON string as TimeSpan", e);
        }
    }

    /// <exception cref="OverflowException"></exception>
    protected abstract TimeSpan intToTimeSpan(double number);

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) {
        // this converter only needs to read, not write
    }

    public class FromSeconds: StringToTimespanConverter {

        /// <inheritdoc />
        protected override TimeSpan intToTimeSpan(double number) => TimeSpan.FromSeconds(number);

    }

    public class FromMilliseconds: StringToTimespanConverter {

        /// <inheritdoc />
        protected override TimeSpan intToTimeSpan(double number) => TimeSpan.FromMilliseconds(number);

    }

}