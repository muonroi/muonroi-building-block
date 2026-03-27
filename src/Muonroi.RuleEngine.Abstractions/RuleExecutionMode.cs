namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Selects how an application routes business logic between traditional code and extracted rules.
/// </summary>
public enum RuleExecutionMode
{
    /// <summary>Execute logic using traditional code only.</summary>
    Traditional = 0,
    /// <summary>Execute logic using extracted rules only.</summary>
    Rules = 1,
    /// <summary>Execute logic using both traditional code and rules, with rules taking precedence.</summary>
    Hybrid = 2,
    /// <summary>Execute logic using traditional code, but run rules in the background and log differences.</summary>
    Shadow = 3
}
