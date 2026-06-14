namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.TraceabilityEntities;

/// <summary>
/// Provenance of an AI-copilot-generated ruleset draft. Captures the original AI output (hash +
/// snapshot) at generation time so the Phase-8 exit metric (VRF-03) can prove whether a human
/// edited the draft before approval and when it was approved.
/// <para>
/// This is the persistence foundation only — write paths land in plan 08-02 and the activation
/// hook in 08-03. The table is tenant-scoped with RLS WITH CHECK (T-08-01); no client input path
/// exists at this tier. <see cref="AiOriginalHash"/> is a server-side SHA256 of the AI draft.
/// </para>
/// </summary>
public sealed class CopilotDraftProvenanceRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name the draft belongs to (max 256).</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>Gets or sets the ruleset version this provenance row describes.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the server-side SHA256 hash of the original AI draft (max 64).</summary>
    public string AiOriginalHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized original AI draft snapshot (text).</summary>
    public string AiOriginalSnapshot { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp the AI draft was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the identifier of the actor/model that generated the draft (max 256).</summary>
    public string GeneratedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the draft was edited by a human before approval. Null until the
    /// activation hook (08-03) resolves it against the approved content.
    /// </summary>
    public bool? EditedBeforeApproval { get; set; }

    /// <summary>Gets or sets the UTC approval timestamp; null until approved.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Gets or sets the id of the originating ingested source document, when this draft was produced
    /// from an ingested document (Phase 17 / TRACE-01). Null = NL-copilot / manual path; non-null =
    /// ingested path. This non-null discriminator is the only ingested-vs-NL signal and powers the
    /// TRACE-02 ingestion edit-rate slice (D-01).
    /// </summary>
    public Guid? SourceDocumentId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable source reference (issue/page key) of the originating ingested
    /// document, for display in the traceability matrix (max 512). Null on the NL/manual path; set
    /// from <c>document.SourceRef</c> on the ingestion path (D-01).
    /// </summary>
    public string? SourceRef { get; set; }
}
