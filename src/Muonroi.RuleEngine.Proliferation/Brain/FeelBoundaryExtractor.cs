using System.Text.Json;
using System.Text.RegularExpressions;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Extracts boundary values from FEEL expressions in a ruleset JSON.
/// Produces n-1/n/n+1 values for comparisons, range boundaries for "between",
/// and true/false branch hints for "if/then/else".
/// </summary>
public static class FeelBoundaryExtractor
{
    // Matches: input.field.path > 100  (or <, >=, <=, =)
    // Group 1 = field path (last segment used as name), Group 2 = operator, Group 3 = numeric value
    private static readonly Regex ComparisonRegex = new(
        @"input\.(\w+(?:\.\w+)*)\s*(>=|<=|>|<|=)\s*(\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches: input.field.path between X and Y
    private static readonly Regex BetweenRegex = new(
        @"input\.(\w+(?:\.\w+)*)\s+between\s+(\d+(?:\.\d+)?)\s+and\s+(\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches: if input.field = value then ... else ...
    private static readonly Regex IfThenElseRegex = new(
        @"if\s+input\.(\w+(?:\.\w+)*)\s*=\s*(\S+)\s+then\s+(.+?)\s+else\s+(.+?)(?:\s*$|"")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extracts boundary values from a FEEL expression string or ruleset JSON.
    /// If the input is valid JSON containing FEEL conditions, those are extracted automatically.
    /// </summary>
    public static BoundaryExtractionResult Extract(string ruleSetJson)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return new BoundaryExtractionResult([], []);

        // Try to parse as JSON and extract all FEEL conditions
        List<string> feelExpressions = TryExtractFeelFromJson(ruleSetJson);

        // If not JSON or no conditions found, treat the whole input as a FEEL expression
        if (feelExpressions.Count == 0)
            feelExpressions.Add(ruleSetJson);

        List<FieldBoundary> boundaries = [];
        List<BranchHint> branches = [];

        foreach (string expression in feelExpressions)
        {
            ExtractFromExpression(expression, boundaries, branches);
        }

        return new BoundaryExtractionResult(boundaries, branches);
    }

    private static List<string> TryExtractFeelFromJson(string json)
    {
        List<string> results = [];

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // Resolve flowGraph wrapper if present
            JsonElement graphRoot = root.TryGetProperty("flowGraph", out JsonElement fg) ? fg : root;

            // Extract from nodes array
            if (graphRoot.TryGetProperty("nodes", out JsonElement nodes)
                && nodes.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement node in nodes.EnumerateArray())
                {
                    if (node.TryGetProperty("data", out JsonElement data))
                    {
                        if (data.TryGetProperty("condition", out JsonElement condEl))
                        {
                            string? cond = condEl.GetString();
                            if (!string.IsNullOrWhiteSpace(cond))
                                results.Add(cond);
                        }
                        if (data.TryGetProperty("expression", out JsonElement exprEl))
                        {
                            string? expr = exprEl.GetString();
                            if (!string.IsNullOrWhiteSpace(expr))
                                results.Add(expr);
                        }
                    }
                }
            }

            // Extract from decision table rules
            if (root.TryGetProperty("rules", out JsonElement rules)
                && rules.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement rule in rules.EnumerateArray())
                {
                    if (rule.TryGetProperty("conditions", out JsonElement conds)
                        && conds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement cond in conds.EnumerateArray())
                        {
                            string? condStr = cond.GetString();
                            if (!string.IsNullOrWhiteSpace(condStr))
                                results.Add(condStr);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON — return empty so caller uses raw input as expression
        }

        return results;
    }

    private static void ExtractFromExpression(
        string expression,
        List<FieldBoundary> boundaries,
        List<BranchHint> branches)
    {
        // Between has higher specificity — match first to avoid double-matching
        foreach (Match m in BetweenRegex.Matches(expression))
        {
            string field = LastSegment(m.Groups[1].Value);
            double lower = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            double upper = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            double midpoint = Math.Round((lower + upper) / 2.0, 2);

            bool isDecimal = m.Groups[2].Value.Contains('.') || m.Groups[3].Value.Contains('.');
            double step = isDecimal ? 0.01 : 1.0;

            List<object> vals =
            [
                lower - step,
                lower,
                midpoint,
                upper,
                upper + step
            ];

            boundaries.Add(new FieldBoundary(field, "between", lower, vals));
        }

        // Remove between-matched segments to avoid double-counting
        string stripped = BetweenRegex.Replace(expression, string.Empty);

        foreach (Match m in ComparisonRegex.Matches(stripped))
        {
            string field = LastSegment(m.Groups[1].Value);
            string op = m.Groups[2].Value;
            double value = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);

            bool isDecimal = m.Groups[3].Value.Contains('.');
            double step = isDecimal ? 0.01 : 1.0;

            List<object> vals = [value - step, value, value + step];
            boundaries.Add(new FieldBoundary(field, op, value, vals));
        }

        // If-then-else branches
        foreach (Match m in IfThenElseRegex.Matches(expression))
        {
            string field = LastSegment(m.Groups[1].Value);
            string conditionValue = m.Groups[2].Value.Trim('"', '\'');
            string truePath = m.Groups[3].Value.Trim('"', '\'', ' ');
            string falsePath = m.Groups[4].Value.Trim('"', '\'', ' ');

            branches.Add(new BranchHint(
                field,
                $"true-path (when {field} = {conditionValue}): {truePath}",
                $"false-path (when {field} != {conditionValue}): {falsePath}"));
        }
    }

    private static string LastSegment(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot >= 0 ? path[(dot + 1)..] : path;
    }
}

/// <summary>
/// Result of FEEL boundary extraction containing comparison boundaries and branch hints.
/// </summary>
public sealed record BoundaryExtractionResult(
    IReadOnlyList<FieldBoundary> Boundaries,
    IReadOnlyList<BranchHint> Branches);

/// <summary>
/// A single field's boundary values derived from a FEEL comparison or between expression.
/// </summary>
public sealed record FieldBoundary(
    string Field,
    string Operator,
    double ThresholdValue,
    IReadOnlyList<object> BoundaryValues);

/// <summary>
/// Describes the two branches of an if/then/else FEEL expression.
/// </summary>
public sealed record BranchHint(
    string Field,
    string TruePath,
    string FalsePath);
