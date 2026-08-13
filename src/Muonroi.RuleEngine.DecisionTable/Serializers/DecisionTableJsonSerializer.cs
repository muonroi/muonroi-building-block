using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Serializers;

/// <summary>
/// Provides JSON serialization and deserialization for decision tables.
/// </summary>
public sealed class DecisionTableJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a decision table to a JSON string.
    /// </summary>
    /// <param name="table">The decision table to serialize.</param>
    /// <returns>A JSON string representation of the decision table.</returns>
    public static string Serialize(DecisionTableModel table)
    {
        return JsonSerializer.Serialize(table, Options); // MBB002-exempt: static helper with custom JsonOptions (WriteIndented + WhenWritingNull) not available in wrapper
    }

    /// <summary>
    /// Deserializes a decision table from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized decision table.</returns>
    /// <exception cref="InvalidDataException">Thrown when the JSON cannot be deserialized.</exception>
    public static DecisionTableModel Deserialize(string json)
    {
        return MGuard.Configured(JsonSerializer.Deserialize<DecisionTableModel>(json, Options), "Cannot deserialize decision table JSON.");
    }
}
