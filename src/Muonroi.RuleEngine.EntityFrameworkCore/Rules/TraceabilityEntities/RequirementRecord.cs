namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.TraceabilityEntities;

/// <summary>
/// A business requirement that a rule (or set of rules) is expected to satisfy.
/// <para>
/// Mirrors the <c>RuleSetRecord</c> tenant boundary + audit-column shape. Requirements are
/// entered manually via API in Phase 3 (no connector), but the entity is import-ready:
/// <see cref="SourceSystem"/> + <see cref="SourceRef"/> carry provenance so a later Jira /
/// Confluence import (INT-01) needs no schema migration (decision D-01).
/// </para>
/// </summary>
public sealed class RequirementRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the requirement title / summary (max 512).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the originating system, when imported (e.g. <c>Jira</c>, <c>Confluence</c>).
    /// Null for manually-entered requirements. Import-ready for INT-01 (decision D-01).
    /// </summary>
    public string? SourceSystem { get; set; }

    /// <summary>
    /// Gets or sets the source reference (Jira key, Confluence URL, meeting-note link).
    /// Free-form. Null for manually-entered requirements (decision D-01).
    /// </summary>
    public string? SourceRef { get; set; }

    /// <summary>Gets or sets the business approver of this requirement, when known (decision D-01).</summary>
    public string? Approver { get; set; }

    /// <summary>Gets or sets the identifier of the user who created this requirement.</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC last-modified timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
