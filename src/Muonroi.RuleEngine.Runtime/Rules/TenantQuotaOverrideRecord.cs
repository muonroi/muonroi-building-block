namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class TenantQuotaOverrideRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = "default";

    public string TargetTenantId { get; set; } = string.Empty;

    public int? MaxRequestsPerDay { get; set; }

    public int? MaxConcurrentRules { get; set; }

    public int? MaxWorkflows { get; set; }

    public string UpdatedBy { get; set; } = "system";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

