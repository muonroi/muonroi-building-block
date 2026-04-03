namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Request payload used to start a canary rollout.
/// </summary>
public sealed class StartCanaryRequest
{
    /// <summary>Workflow name to target.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Ruleset version to activate in canary.</summary>
    public int Version { get; set; }

    /// <summary>Optional tenant identifiers to target.</summary>
    public string[] TargetTenantIds { get; set; } = [];

    /// <summary>Optional percentage of traffic to route to canary.</summary>
    public int? TargetPercentage { get; set; }

    /// <summary>Identifier of the user that started the canary.</summary>
    public string StartedBy { get; set; } = "system";
}

