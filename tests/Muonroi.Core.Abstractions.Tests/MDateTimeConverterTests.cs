namespace Muonroi.Core.Abstractions.Tests;

public class MDateTimeConverterTests
{
    [Fact]
    public void Read_Parses_Valid_String()
    {
        string text = "2024-01-01T00:00:00Z";
        byte[] data = Encoding.UTF8.GetBytes("\"" + text + "\"");
        Utf8JsonReader reader = new(data);
        _ = reader.Read();

        MDateTimeConverter converter = new();
        DateTime result = converter.Read(ref reader, typeof(DateTime), new JsonSerializerOptions());

        Assert.Equal(
            DateTime.Parse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void Read_Null_String_Returns_Default()
    {
        byte[] data = Encoding.UTF8.GetBytes("null");
        Utf8JsonReader reader = new(data);
        _ = reader.Read();

        MDateTimeConverter converter = new();
        DateTime result = converter.Read(ref reader, typeof(DateTime), new JsonSerializerOptions());

        Assert.Equal(default, result);
    }

    [Fact]
    public void Read_Invalid_Format_Throws()
    {
        Assert.ThrowsAny<JsonException>(() =>
        {
            byte[] data = Encoding.UTF8.GetBytes("\"bad\"");
            Utf8JsonReader reader = new(data);
            _ = reader.Read();

            MDateTimeConverter converter = new();
            converter.Read(ref reader, typeof(DateTime), new JsonSerializerOptions());
        });
    }

    [Fact]
    public void Write_Serializes_As_Utc()
    {
        MDateTimeConverter converter = new();
        DateTime value = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream);
        converter.Write(writer, value, new JsonSerializerOptions());
        writer.Flush();

        string json = Encoding.UTF8.GetString(stream.ToArray());
        string expected = "\"" + value.ToString("O", CultureInfo.InvariantCulture) + "\"";
        Assert.Equal(expected, json);
    }
}
