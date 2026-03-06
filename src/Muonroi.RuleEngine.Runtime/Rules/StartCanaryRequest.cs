namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class StartCanaryRequest
{
    public string WorkflowName { get; set; } = string.Empty;

    public int Version { get; set; }

    public string[] TargetTenantIds { get; set; } = [];

    public int? TargetPercentage { get; set; }

    public string StartedBy { get; set; } = "system";
}

