namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Provides JSON serialization and deserialization services.
/// </summary>
public interface IMJsonSerializeService
{
    /// <summary>
    /// Serializes the specified object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    string Serialize<T>(T obj);

    /// <summary>
    /// Deserializes the specified JSON string to an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="text">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    T? Deserialize<T>(string text);
}
