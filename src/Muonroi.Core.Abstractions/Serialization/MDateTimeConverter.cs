namespace Muonroi.Core.Abstractions.Serialization;

/// <summary>
/// A JSON converter for <see cref="DateTime"/> that ensures UTC format.
/// </summary>
public class MDateTimeConverter
    : JsonConverter<DateTime>
{
    /// <summary>
    /// Reads and converts a JSON string to a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>A UTC <see cref="DateTime"/>.</returns>
    /// <exception cref="JsonException">Thrown when the date format is invalid.</exception>
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

    /// <summary>
    /// Writes a <see cref="DateTime"/> as a UTC JSON string.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The <see cref="DateTime"/> value to write.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }
}
