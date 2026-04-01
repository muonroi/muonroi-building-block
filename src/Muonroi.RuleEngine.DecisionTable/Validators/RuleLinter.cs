using System.Text.Json;
using Muonroi.RuleEngine.Abstractions.Models;

namespace Muonroi.RuleEngine.DecisionTable.Validators;

/// <summary>
/// Provides methods for linting rule engine definitions.
/// </summary>
public static class RuleLinter
{
    /// <summary>
    /// Lints a rule definition file.
    /// </summary>
    /// <param name="path">The path to the JSON rule file.</param>
    /// <returns>A collection of linting messages.</returns>
    public static IEnumerable<LintMessage> LintFile(string path)
    {
        return Lint(File.ReadAllText(path));
    }

    /// <summary>
    /// Lints a JSON string representing a rule definition.
    /// </summary>
    /// <param name="json">The JSON string to lint.</param>
    /// <returns>A collection of linting messages.</returns>
    public static IEnumerable<LintMessage> Lint(string json)
    {
        List<LintMessage> messages = [];
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            messages.Add(new LintMessage("LINT_INVALID_JSON", ex.Message, LintSeverity.Error));
            return messages;
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            messages.Add(new LintMessage("LINT_INVALID_TYPE", "Root element must be an object", LintSeverity.Error));
            return messages;
        }

        JsonElement root = doc.RootElement;
        string? hitPolicy = root.TryGetProperty("hitPolicy", out JsonElement hp) ? hp.GetString() : null;
        if (!root.TryGetProperty("rules", out JsonElement rulesElement) || rulesElement.ValueKind != JsonValueKind.Array)
        {
            messages.Add(new LintMessage("LINT_MISSING_FIELD", "rules", LintSeverity.Error));
            return messages;
        }

        HashSet<string> ids = [];
        List<(double Min, double Max, string Id)> ranges = [];
        foreach (JsonElement rule in rulesElement.EnumerateArray())
        {
            if (rule.ValueKind != JsonValueKind.Object)
            {
                messages.Add(new LintMessage("LINT_INVALID_TYPE", "rule must be object", LintSeverity.Error));
                continue;
            }

            string? id = rule.TryGetProperty("id", out JsonElement idProp) ? idProp.GetString() : null;
            if (string.IsNullOrEmpty(id))
                messages.Add(new LintMessage("LINT_MISSING_FIELD", "id", LintSeverity.Error));
            else if (!ids.Add(id)) messages.Add(new LintMessage("LINT_DUPLICATE_ID", id, LintSeverity.Error));

            if (!rule.TryGetProperty("outputs", out JsonElement outputsProp) || outputsProp.ValueKind != JsonValueKind.Object ||
                outputsProp.GetRawText() == "{}")
                messages.Add(new LintMessage("LINT_MISSING_OUTPUT", id ?? "<unknown>", LintSeverity.Error));

            if (rule.TryGetProperty("range", out JsonElement rangeProp) && rangeProp.ValueKind == JsonValueKind.Object)
            {
                if (rangeProp.TryGetProperty("min", out JsonElement minProp) && rangeProp.TryGetProperty("max", out JsonElement maxProp) &&
                    minProp.TryGetDouble(out double min) && maxProp.TryGetDouble(out double max))
                    ranges.Add((min, max, id ?? string.Empty));
                else
                    messages.Add(new LintMessage("LINT_INVALID_TYPE", "range.min/max", LintSeverity.Error));
            }
        }

        if (hitPolicy is "FIRST" or "UNIQUE")
            for (int i = 0; i < ranges.Count; i++)
            for (int j = i + 1; j < ranges.Count; j++)
            {
                (double min, double max, string id) = ranges[i];
                (double Min, double Max, string Id) = ranges[j];
                if (min <= Max && Min <= max)
                    messages.Add(new LintMessage("LINT_OVERLAP_RANGE", $"{id} overlaps {Id}", LintSeverity.Warning));
            }

        return messages;
    }
}
