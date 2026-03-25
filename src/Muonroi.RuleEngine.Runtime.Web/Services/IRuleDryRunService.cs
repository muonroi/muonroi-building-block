namespace Muonroi.RuleEngine.Runtime.Web.Services;

/// <summary>
/// Executes ruleset dry-runs and returns traces and output facts.
/// </summary>
public interface IRuleDryRunService
{
    /// <summary>Runs a ruleset dry-run with the supplied inputs.</summary>
    /// <param name="ruleSetContent">Ruleset payload.</param>
    /// <param name="format">Ruleset format.</param>
    /// <param name="inputFacts">Input facts for evaluation.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dry-run result.</returns>
    Task<RuleDryRunResult> RunAsync(
        string ruleSetContent,
        RuleSetFormat format,
        IReadOnlyDictionary<string, object?> inputFacts,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supported ruleset payload formats for dry-run.
/// </summary>
public enum RuleSetFormat
{
    /// <summary>JSON ruleset format.</summary>
    Json = 0,
    /// <summary>XML ruleset format.</summary>
    Xml = 1,
    /// <summary>DMN ruleset format.</summary>
    Dmn = 2
}

/// <summary>
/// Result of a ruleset dry-run execution.
/// </summary>
public sealed class RuleDryRunResult
{
    /// <summary>True when at least one rule matched and no errors were produced.</summary>
    public bool RulesMatched { get; init; }
    /// <summary>Execution traces per rule.</summary>
    public IReadOnlyList<RuleExecutionTrace> Traces { get; init; } = [];
    /// <summary>Total evaluation time.</summary>
    public TimeSpan EvaluationTime { get; init; }
    /// <summary>Errors produced during evaluation.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
    /// <summary>Output facts produced by rule actions.</summary>
    public IReadOnlyDictionary<string, object?> OutputFacts { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Per-rule execution trace used in dry-run responses.
/// </summary>
public sealed class RuleExecutionTrace
{
    /// <summary>Rule name.</summary>
    public string RuleName { get; init; } = string.Empty;
    /// <summary>True when the rule matched.</summary>
    public bool Matched { get; init; }
    /// <summary>Failure reason when the rule did not match.</summary>
    public string? FailReason { get; init; }
    /// <summary>JSON string of FactBag state the rule received at evaluation time.</summary>
    public string? InputFactsJson { get; init; }
    /// <summary>JSON string of FactBag state after this rule completed execution.</summary>
    public string? OutputFactsJson { get; init; }
    /// <summary>Keys that were added or changed by this rule during execution.</summary>
    public IReadOnlyList<string>? ChangedFactKeys { get; init; }
    /// <summary>Execution time in milliseconds for this specific rule.</summary>
    public long? ElapsedMs { get; init; }
}
