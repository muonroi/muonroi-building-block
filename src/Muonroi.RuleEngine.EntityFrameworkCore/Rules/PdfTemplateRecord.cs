namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Header record for a PDF template.  One row per logical template; versioned content
/// rows live in <see cref="PdfTemplateVersionRecord"/>.
/// <para>
/// Mirrors the <c>RuleSetRecord</c> shape: same tenant boundary, lifecycle actor columns,
/// and approval state.  The human-facing key is <see cref="TemplateId"/> (string), which
/// maps to <c>TemplateDescriptor.TemplateId</c> in <c>IMPdfTemplateRegistry</c>.
/// </para>
/// </summary>
public sealed class PdfTemplateRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>
    /// Gets or sets the human-facing template identifier (max 256).
    /// Unique within a tenant.  Corresponds to <c>TemplateDescriptor.TemplateId</c>.
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name of the template (max 256).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the current lifecycle status of the template header.</summary>
    public PdfTemplateStatus Status { get; set; } = PdfTemplateStatus.Draft;

    /// <summary>
    /// Gets or sets a value indicating whether there is currently an active (published) version.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PdfTemplateVersionRecord.Id"/> of the currently published version,
    /// or <c>null</c> if no version has been activated yet.
    /// </summary>
    public Guid? CurrentVersionId { get; set; }

    /// <summary>Gets or sets the identifier of the user who created this template.</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>Gets or sets the identifier of the user who last submitted for approval.</summary>
    public string? SubmittedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last submission.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who approved the last submission.</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last approval.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who rejected the last submission.</summary>
    public string? RejectedBy { get; set; }

    /// <summary>Gets or sets the rejection reason text (free-text column).</summary>
    public string? RejectedReason { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC last-modified timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
