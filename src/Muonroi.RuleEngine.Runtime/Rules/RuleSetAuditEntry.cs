namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Represents a paged ruleset audit entry.
/// </summary>
public sealed class RuleSetAuditEntry
{
    /// <summary>Gets or sets the audit entry identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the UTC timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the tenant identifier.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the target tenant identifier.</summary>
    public string? TargetTenantId { get; set; }

    /// <summary>Gets or sets the action name.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the version number.</summary>
    public int? Version { get; set; }

    /// <summary>Gets or sets the actor name.</summary>
    public string? Actor { get; set; }

    /// <summary>Gets or sets the detail text.</summary>
    public string? Detail { get; set; }

    /// <summary>Gets or sets the content hash.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Gets or sets the signature algorithm.</summary>
    public string? SignatureAlgorithm { get; set; }

    /// <summary>Gets or sets the signature key identifier.</summary>
    public string? SignatureKeyId { get; set; }

    /// <summary>Gets or sets the signature payload.</summary>
    public string? Signature { get; set; }
}
