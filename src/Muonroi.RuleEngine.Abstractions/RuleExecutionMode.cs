namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Selects how an application routes business logic between traditional code and extracted rules.
/// </summary>
public enum RuleExecutionMode
{
    Traditional = 0,
    Rules = 1,
    Hybrid = 2,
    Shadow = 3
}
