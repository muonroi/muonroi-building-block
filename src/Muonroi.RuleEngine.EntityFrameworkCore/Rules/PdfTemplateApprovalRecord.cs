namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Immutable approval-event record for a <see cref="PdfTemplateVersionRecord"/>.
/// One row is appended per approval lifecycle transition (submit / approve / reject),
/// providing a tamper-evident audit trail separate from the mutable inline columns
/// on <see cref="PdfTemplateVersionRecord"/>.
/// <para>
/// Mirrors the intent of <c>RuleSetApprovalService</c> event logging while giving
/// PDF templates a dedicated, queryable approval history table.
/// </para>
/// </summary>
public sealed class PdfTemplateApprovalRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>
    /// Gets or sets the FK to <see cref="PdfTemplateVersionRecord.Id"/>.
    /// </summary>
    public Guid TemplateVersionId { get; set; }

    /// <summary>
    /// Gets or sets the human-facing template identifier (max 256).
    /// Denormalised for efficient tenant-scoped queries without a join.
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Gets or sets the version number of the associated template version.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the identifier of the user who submitted this version for approval.</summary>
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp of submission.</summary>
    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who approved the version, or <c>null</c>.</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp of approval, or <c>null</c>.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who rejected the version, or <c>null</c>.</summary>
    public string? RejectedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp of rejection, or <c>null</c>.</summary>
    public DateTimeOffset? RejectedAt { get; set; }

    /// <summary>Gets or sets the rejection reason text (free-text column), or <c>null</c>.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp of this approval record.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
