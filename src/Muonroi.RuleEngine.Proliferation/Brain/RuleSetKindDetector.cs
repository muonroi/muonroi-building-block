using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Classifies a ruleset JSON into a kind for prompt template selection.
/// </summary>
public enum RuleSetKind
{
    /// <summary>Unrecognized or empty ruleset payload.</summary>
    Unknown = 0,
    /// <summary>Flow graph ruleset payload.</summary>
    FlowGraph = 1,
    /// <summary>Decision table ruleset payload.</summary>
    DecisionTable = 2,
    /// <summary>Code-based ruleset payload.</summary>
    CodeBased = 3,
    /// <summary>Legacy ruleset payload.</summary>
    Legacy = 4
}

/// <summary>
/// Detects the kind of ruleset encoded in JSON.
/// </summary>
public static class RuleSetKindDetector
{
    /// <summary>
    /// Analyzes ruleset JSON structure to determine its kind.
    /// </summary>
    public static RuleSetKind Detect(string ruleSetJson)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return RuleSetKind.Unknown;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetJson);
            JsonElement root = doc.RootElement;

            // Flow graph: has "flowGraph", "nodes", or "edges" properties
            if (root.TryGetProperty("flowGraph", out _)
                || (root.TryGetProperty("nodes", out _) && root.TryGetProperty("edges", out _)))
            {
                return RuleSetKind.FlowGraph;
            }

            // Decision table: has "hitPolicy" or "inputColumns"
            if (root.TryGetProperty("hitPolicy", out _)
                || root.TryGetProperty("inputColumns", out _))
            {
                return RuleSetKind.DecisionTable;
            }

            // Code-based: has "rules" array with entries containing "ruleCode"
            if (root.TryGetProperty("rules", out JsonElement rules)
                && rules.ValueKind == JsonValueKind.Array
                && rules.GetArrayLength() > 0)
            {
                JsonElement firstRule = rules[0];
                if (firstRule.TryGetProperty("ruleCode", out _))
                {
                    return RuleSetKind.CodeBased;
                }
            }

            // Legacy: has some structure but doesn't match above patterns
            if (root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any())
            {
                return RuleSetKind.Legacy;
            }

            return RuleSetKind.Unknown;
        }
        catch (JsonException)
        {
            return RuleSetKind.Unknown;
        }
    }
}
