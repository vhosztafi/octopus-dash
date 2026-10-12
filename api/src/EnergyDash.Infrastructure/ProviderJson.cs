using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnergyDash.Infrastructure;

public static class ProviderJson
{
    // Supplier responses use both Z and explicit offsets. DateTime's default converter can
    // otherwise produce host-local values; every provider timestamp must enter the domain in UTC.
    public static readonly JsonSerializerOptions Options = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new UtcDateTimeConverter());
        return options;
    }
    private sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDateTimeOffset().UtcDateTime;
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToUniversalTime());
    }
}
