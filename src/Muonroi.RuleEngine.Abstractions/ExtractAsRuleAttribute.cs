namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Backward-compatible alias for <see cref="MExtractAsRuleAttribute"/>.
/// </summary>
public sealed class ExtractAsRuleAttribute(string code) : MExtractAsRuleAttribute(code);
