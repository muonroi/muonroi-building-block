namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Versioned content record for a PDF template.  Each submit/approve cycle produces
/// a new row; the parent header is tracked in <see cref="PdfTemplateRecord"/>.
/// <para>
/// The tuple <c>(TenantId, TemplateId, Version)</c> is unique, mirroring the
/// <c>(TenantId, WorkflowName, Version)</c> index on <c>RuleSetRecord</c>.
/// </para>
/// </summary>
public sealed class PdfTemplateVersionRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>
    /// Gets or sets the human-facing template identifier (max 256).
    /// Matches <see cref="PdfTemplateRecord.TemplateId"/>.
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Gets or sets the monotonically-incrementing version number.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the serialised HTML/CSS content of this template version
    /// (column type: text).
    /// </summary>
    public string ContentJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME content type of <see cref="ContentJson"/>
    /// (e.g. <c>text/html</c>).  Passed through to <c>TemplateVersion.ContentType</c>.
    /// </summary>
    public string ContentType { get; set; } = "text/html";

    /// <summary>Gets or sets the lifecycle status of this version.</summary>
    public PdfTemplateStatus Status { get; set; } = PdfTemplateStatus.Draft;

    /// <summary>Gets or sets the identifier of the user who created this version.</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>Gets or sets the identifier of the user who submitted this version for approval.</summary>
    public string? SubmittedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this version was submitted for approval.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who approved this version.</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this version was approved.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who rejected this version.</summary>
    public string? RejectedBy { get; set; }

    /// <summary>Gets or sets the rejection reason text (free-text column).</summary>
    public string? RejectedReason { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this version was published (activated).</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC last-modified timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
