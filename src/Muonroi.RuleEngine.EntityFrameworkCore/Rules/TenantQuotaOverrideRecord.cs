namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Per-tenant quota override settings.
/// </summary>
public sealed class TenantQuotaOverrideRecord
{
    /// <summary>Record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant identifier that owns the override.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Target tenant identifier for the override.</summary>
    public string TargetTenantId { get; set; } = string.Empty;

    /// <summary>Maximum requests per day allowed.</summary>
    public int? MaxRequestsPerDay { get; set; }

    /// <summary>Maximum concurrent rules allowed.</summary>
    public int? MaxConcurrentRules { get; set; }

    /// <summary>Maximum number of workflows allowed.</summary>
    public int? MaxWorkflows { get; set; }

    /// <summary>Identifier of the user who updated the override.</summary>
    public string UpdatedBy { get; set; } = "system";

    /// <summary>Timestamp of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

