using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Serializers;

public sealed class DecisionTableJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(DecisionTableModel table)
    {
        return JsonSerializer.Serialize(table, Options); // MBB002-exempt: static helper with custom JsonOptions (WriteIndented + WhenWritingNull) not available in wrapper
    }

    public static DecisionTableModel Deserialize(string json)
    {
        return JsonSerializer.Deserialize<DecisionTableModel>(json, Options) // MBB002-exempt: static helper with custom JsonOptions (WriteIndented + WhenWritingNull) not available in wrapper
               ?? throw new InvalidDataException("Cannot deserialize decision table JSON.");
    }
}
