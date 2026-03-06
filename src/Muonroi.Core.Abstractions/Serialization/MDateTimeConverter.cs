namespace Muonroi.Core.Abstractions.Serialization;

public class MDateTimeConverter
    : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? dateString = reader.GetString();

        if (string.IsNullOrWhiteSpace(dateString))
        {
            return default;
        }

        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsedDate))
        {
            return parsedDate.Kind == DateTimeKind.Utc
                ? parsedDate
                : DateTime.SpecifyKind(parsedDate.ToUniversalTime(), DateTimeKind.Utc);
        }

        throw new JsonException($"Invalid date format: {dateString}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }
}
