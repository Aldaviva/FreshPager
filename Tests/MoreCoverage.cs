using FreshPager.Data.Marshal;
using System.Text.Json;

namespace Tests;

public class MoreCoverage {

    [Fact]
    public void iWantMoreCoverage() {
        static void formatException() {
            Utf8JsonReader reader = new("\"abc\""u8);
            reader.Read();
            new StringToTimespanConverter.FromSeconds().Read(ref reader, typeof(TimeSpan), JsonSerializerOptions.Default);
        }

        ((Action) formatException).Should().Throw<JsonException>();

        static void overflow() {
            Utf8JsonReader reader = new("\"99999999999999999999999999999999999999999\""u8);
            reader.Read();
            new StringToTimespanConverter.FromSeconds().Read(ref reader, typeof(TimeSpan), JsonSerializerOptions.Default);
        }

        ((Action) overflow).Should().Throw<JsonException>();

        new StringToOptionalIntConverter().Write(null!, null, JsonSerializerOptions.Default);
        new StringToTimespanConverter.FromSeconds().Write(null!, TimeSpan.Zero, JsonSerializerOptions.Default);

    }

}