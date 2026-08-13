namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Backward-compatible alias for <see cref="MRuleModeAttribute"/>.
/// </summary>
public sealed class RuleModeAttribute(RuleExecutionMode mode) : MRuleModeAttribute(mode);
