namespace Muonroi.RuleEngine.Testing;

public sealed record MRuleExecutionRecord(
    string RuleCode,
    bool IsSuccess,
    TimeSpan Duration,
    IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> Changes);
