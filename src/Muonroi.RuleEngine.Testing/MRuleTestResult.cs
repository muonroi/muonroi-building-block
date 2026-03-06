namespace Muonroi.RuleEngine.Testing;

/// <summary>
/// Result object returned by <see cref="MRuleTestBuilder{TContext}"/>.
/// </summary>
public sealed record MRuleTestResult(
    bool IsSuccess,
    FactBag Facts,
    RuleResult? RuleResult = null,
    Exception? Exception = null,
    IReadOnlyList<string>? ExecutedRuleCodes = null);
