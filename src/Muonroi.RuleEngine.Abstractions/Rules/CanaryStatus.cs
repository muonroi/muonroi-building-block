namespace Muonroi.RuleEngine.Abstractions.Rules;

/// <summary>
/// Status of a canary rollout.
/// </summary>
public enum CanaryStatus
{
    /// <summary>Rollout is active.</summary>
    Active = 0,
    /// <summary>Rollout was promoted.</summary>
    Promoted = 1,
    /// <summary>Rollout was rolled back.</summary>
    RolledBack = 2
}

