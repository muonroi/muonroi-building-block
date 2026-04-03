namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Parsed cell expression metadata used by validators and overlap/gap detection.
/// </summary>
public sealed class CellExpression
{
    /// <summary>Original expression string.</summary>
    public string Raw { get; init; } = string.Empty;
    /// <summary>True when the expression matches any value.</summary>
    public bool IsWildcard { get; init; }
    /// <summary>True when the expression represents a numeric range.</summary>
    public bool IsRange { get; init; }
    /// <summary>True when the range includes the minimum value.</summary>
    public bool IncludeMin { get; init; } = true;
    /// <summary>True when the range includes the maximum value.</summary>
    public bool IncludeMax { get; init; } = true;
    /// <summary>Minimum value of the range, if any.</summary>
    public double? Min { get; init; }
    /// <summary>Maximum value of the range, if any.</summary>
    public double? Max { get; init; }
    /// <summary>Explicit list of allowed values.</summary>
    public IReadOnlyList<string> Values { get; init; } = [];
}
