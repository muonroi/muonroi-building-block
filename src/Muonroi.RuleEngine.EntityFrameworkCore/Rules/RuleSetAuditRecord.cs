namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Entity stored for ruleset audit events.
/// </summary>
public sealed class RuleSetAuditRecord
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the target tenant identifier.</summary>
    public string? TargetTenantId { get; set; }

    /// <summary>Gets or sets the version number.</summary>
    public int? Version { get; set; }

    /// <summary>Gets or sets the audit event type.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor name.</summary>
    public string Actor { get; set; } = "system";

    /// <summary>Gets or sets the detail text.</summary>
    public string? Detail { get; set; }

    /// <summary>Gets or sets the UTC occurrence time.</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the content hash.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the signature algorithm.</summary>
    public string SignatureAlgorithm { get; set; } = string.Empty;

    /// <summary>Gets or sets the signature key identifier.</summary>
    public string SignatureKeyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the signature payload.</summary>
    public string Signature { get; set; } = string.Empty;
}
