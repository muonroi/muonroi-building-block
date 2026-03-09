namespace Muonroi.RuleEngine.Runtime.Web.Services;

public interface IRuleDryRunService
{
    Task<RuleDryRunResult> RunAsync(
        string ruleSetContent,
        RuleSetFormat format,
        IReadOnlyDictionary<string, object?> inputFacts,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

public enum RuleSetFormat
{
    Json = 0,
    Xml = 1,
    Dmn = 2
}

public sealed class RuleDryRunResult
{
    public bool RulesMatched { get; init; }
    public IReadOnlyList<RuleExecutionTrace> Traces { get; init; } = [];
    public TimeSpan EvaluationTime { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyDictionary<string, object?> OutputFacts { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RuleExecutionTrace
{
    public string RuleName { get; init; } = string.Empty;
    public bool Matched { get; init; }
    public string? FailReason { get; init; }
}
