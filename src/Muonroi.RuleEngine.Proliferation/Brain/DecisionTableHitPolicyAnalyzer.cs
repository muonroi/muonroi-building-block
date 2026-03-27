using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Analyzes a decision table ruleset JSON and produces hit-policy-specific
/// scenario generation hints for the proliferation engine.
/// </summary>
public static class DecisionTableHitPolicyAnalyzer
{
    /// <summary>
    /// Parses the decision table JSON and returns hit-policy analysis with scenario hints.
    /// Returns a default (empty) analysis for null/empty/invalid input.
    /// </summary>
    public static HitPolicyAnalysis Analyze(string ruleSetJson)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return HitPolicyAnalysis.Empty;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetJson);
            JsonElement root = doc.RootElement;

            // Extract hit policy (default: First)
            string hitPolicy = "First";
            if (root.TryGetProperty("hitPolicy", out JsonElement policyEl))
            {
                string? val = policyEl.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                    hitPolicy = val;
            }

            // Extract input column names
            List<string> inputColumns = [];
            if (root.TryGetProperty("inputColumns", out JsonElement colsEl)
                && colsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement col in colsEl.EnumerateArray())
                {
                    if (col.TryGetProperty("name", out JsonElement nameEl))
                    {
                        string? name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            inputColumns.Add(name);
                    }
                }
            }

            // Count rows
            int rowCount = 0;
            if (root.TryGetProperty("rules", out JsonElement rulesEl)
                && rulesEl.ValueKind == JsonValueKind.Array)
            {
                rowCount = rulesEl.GetArrayLength();
            }

            // Generate policy-specific hints
            List<string> hints = BuildPolicyHints(hitPolicy, rowCount);

            return new HitPolicyAnalysis(hitPolicy, rowCount, inputColumns, hints);
        }
        catch (JsonException)
        {
            return HitPolicyAnalysis.Empty;
        }
    }

    private static List<string> BuildPolicyHints(string hitPolicy, int rowCount)
    {
        return hitPolicy.ToUpperInvariant() switch
        {
            "FIRST" => BuildFirstHints(rowCount),
            "UNIQUE" => BuildUniqueHints(rowCount),
            "COLLECT" => BuildCollectHints(rowCount),
            "PRIORITY" => BuildPriorityHints(rowCount),
            _ => BuildFirstHints(rowCount) // default to First behavior
        };
    }

    private static List<string> BuildFirstHints(int rowCount)
    {
        List<string> hints =
        [
            "Test row ordering — first matching row wins; generate inputs that match multiple rows to verify first-match-wins behavior.",
            "Generate inputs that match only the last row to ensure all rows are reachable despite early-exit semantics."
        ];

        if (rowCount >= 3)
            hints.Add($"With {rowCount} rows, create overlapping inputs (rows 1 and {rowCount}) to confirm the first match takes precedence.");

        return hints;
    }

    private static List<string> BuildUniqueHints(int rowCount)
    {
        return
        [
            "Generate overlapping inputs to test uniqueness constraint — inputs that match multiple rows should trigger a uniqueness violation.",
            "Generate non-overlapping inputs for each row to verify the table covers all distinct cases.",
            "Test gap inputs (matching no row) to verify undefined-behavior handling when no unique match exists."
        ];
    }

    private static List<string> BuildCollectHints(int rowCount)
    {
        List<string> hints =
        [
            "Generate inputs that simultaneously match multiple rows to verify aggregation of all matching results.",
            "Test inputs matching only one row to verify single-result collection behavior.",
            "Test inputs matching NO rows to verify empty-collection handling."
        ];

        if (rowCount >= 4)
            hints.Add($"With {rowCount} rows, generate inputs matching rows [1, 2, {rowCount}] simultaneously to verify full aggregation.");

        return hints;
    }

    private static List<string> BuildPriorityHints(int rowCount)
    {
        return
        [
            "Generate inputs matching different-priority rows to verify that higher-priority rows win over lower-priority rows.",
            "Test priority ordering with inputs that match rows having equal priority to verify deterministic selection.",
            "Generate inputs matching only the lowest-priority row to ensure it is reachable."
        ];
    }
}

/// <summary>
/// Result of analyzing a decision table's hit policy.
/// Contains the policy name, row count, input columns, and policy-specific scenario hints.
/// </summary>
public sealed record HitPolicyAnalysis(
    string HitPolicy,
    int RowCount,
    IReadOnlyList<string> InputColumns,
    IReadOnlyList<string> PolicyHints)
{
    /// <summary>Default empty analysis returned for invalid/null inputs.</summary>
    public static readonly HitPolicyAnalysis Empty =
        new("First", 0, [], []);
}
