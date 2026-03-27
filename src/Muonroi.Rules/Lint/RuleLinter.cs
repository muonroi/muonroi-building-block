namespace Muonroi.Rules.Lint;

/// <summary>
/// Defines the severity levels for linting messages.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public enum LintSeverity
{
    /// <summary>Indicates a potential issue that does not prevent execution.</summary>
    Warning,
    /// <summary>Indicates a critical issue that must be resolved.</summary>
    Error
}

/// <summary>
/// Represents a message generated during the linting process.
/// </summary>
/// <param name="Code">The unique error or warning code.</param>
/// <param name="Message">The descriptive message.</param>
/// <param name="Severity">The severity level of the message.</param>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public record LintMessage(string Code, string Message, LintSeverity Severity);

/// <summary>
/// Provides methods for linting rule engine definitions.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
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
        string? hitPolicy = root.GetPropertyOrDefault("hitPolicy")?.GetString();
        JsonElement? rulesElement = root.GetPropertyOrDefault("rules");
        if (rulesElement is null || rulesElement.Value.ValueKind != JsonValueKind.Array)
        {
            messages.Add(new LintMessage("LINT_MISSING_FIELD", "rules", LintSeverity.Error));
            return messages;
        }

        HashSet<string> ids = [];
        List<(double Min, double Max, string Id)> ranges = [];
        foreach (JsonElement rule in rulesElement.Value.EnumerateArray())
        {
            if (rule.ValueKind != JsonValueKind.Object)
            {
                messages.Add(new LintMessage("LINT_INVALID_TYPE", "rule must be object", LintSeverity.Error));
                continue;
            }

            JsonElement? idProp = rule.GetPropertyOrDefault("id");
            string? id = idProp?.GetString();
            if (string.IsNullOrEmpty(id))
                messages.Add(new LintMessage("LINT_MISSING_FIELD", "id", LintSeverity.Error));
            else if (!ids.Add(id)) messages.Add(new LintMessage("LINT_DUPLICATE_ID", id, LintSeverity.Error));

            JsonElement? outputsProp = rule.GetPropertyOrDefault("outputs");
            if (outputsProp is null || outputsProp.Value.ValueKind != JsonValueKind.Object ||
                outputsProp.Value.GetRawText() == "{}")
                messages.Add(new LintMessage("LINT_MISSING_OUTPUT", id ?? "<unknown>", LintSeverity.Error));

            JsonElement? rangeProp = rule.GetPropertyOrDefault("range");
            if (rangeProp is not null && rangeProp.Value.ValueKind == JsonValueKind.Object)
            {
                JsonElement? minProp = rangeProp.Value.GetPropertyOrDefault("min");
                JsonElement? maxProp = rangeProp.Value.GetPropertyOrDefault("max");
                if (minProp is not null && maxProp is not null &&
                    minProp.Value.TryGetDouble(out double min) && maxProp.Value.TryGetDouble(out double max))
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

    private static JsonElement? GetPropertyOrDefault(this JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) ? value : null;
    }
}
