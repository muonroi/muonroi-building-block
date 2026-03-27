namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for JSON serialization.
/// </summary>
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions _option = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes the specified object to a JSON string.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A JSON string representation of the object, or an empty string if serialization fails.</returns>
    public static string Serialize(this object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, _option); // MBB002-exempt: static-class boundary
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
