namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Parsed cell expression metadata used by validators and overlap/gap detection.
/// </summary>
public sealed class CellExpression
{
    public string Raw { get; init; } = string.Empty;
    public bool IsWildcard { get; init; }
    public bool IsRange { get; init; }
    public bool IncludeMin { get; init; } = true;
    public bool IncludeMax { get; init; } = true;
    public double? Min { get; init; }
    public double? Max { get; init; }
    public IReadOnlyList<string> Values { get; init; } = [];
}
