namespace Muonroi.RuleEngine.Abstractions.Models;

/// <summary>
/// Defines the hit policies for raw decision tables (legacy/import support).
/// </summary>
public enum RawHitPolicy
{
    /// <summary>
    /// The first rule that matches is used.
    /// </summary>
    First,

    /// <summary>
    /// Only one rule should match; otherwise, an error occurs.
    /// </summary>
    Unique
}

/// <summary>
/// Represents a single rule in a raw decision table.
/// </summary>
/// <param name="Inputs">The input values for the rule.</param>
/// <param name="Outputs">The output values for the rule.</param>
public record RawDecisionRule(Dictionary<string, string> Inputs, Dictionary<string, string> Outputs);

/// <summary>
/// Represents a raw decision table structure for import/export operations.
/// </summary>
public class RawDecisionTable
{
    /// <summary>
    /// Gets or sets the hit policy for the decision table.
    /// </summary>
    public RawHitPolicy HitPolicy { get; set; }

    /// <summary>
    /// Gets the list of input headers.
    /// </summary>
    public List<string> InputHeaders { get; } = [];

    /// <summary>
    /// Gets the list of output headers.
    /// </summary>
    public List<string> OutputHeaders { get; } = [];

    /// <summary>
    /// Gets the list of rules in the decision table.
    /// </summary>
    public List<RawDecisionRule> Rules { get; } = [];

    /// <summary>
    /// Gets the list of warnings generated during import.
    /// </summary>
    public List<string> Warnings { get; } = [];
}
