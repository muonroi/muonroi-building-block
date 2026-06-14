namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.IngestedSourceEntities;

/// <summary>
/// Persisted artifact of a single connector ingestion run. Stores ONLY the redacted+normalized
/// content — raw document body is NEVER persisted (D-04/D-06, Phase 15). The <see cref="RedactedContent"/>
/// column holds the output of <c>IPromptRedactor.RedactText</c>; <see cref="NormalizedContent"/>
/// holds the plaintext extracted from the connector response before redaction. There is intentionally
/// no raw-body column — raw body is structurally impossible to persist on this entity (T-15-07).
/// <para>
/// Tenant-scoped with RLS ENABLE+FORCE and a <c>tenant_isolation</c> policy carrying both USING and
/// WITH CHECK keyed on <c>current_setting('app.current_tenant_id', true)</c> (D-08/T-15-01/T-15-02).
/// Isolation is enforced by <c>TenantRlsConnectionInterceptor</c> — no new interceptor is needed (D-09).
/// </para>
/// </summary>
public sealed class IngestedSourceDocumentRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the connector identifier that sourced this document (max 128).</summary>
    public string ConnectorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the source reference (Jira key, Confluence pageId, file handle — max 512).</summary>
    public string SourceRef { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the redacted content — the output of <c>IPromptRedactor.RedactText</c>. This is
    /// the exact text handed to any downstream surface (dashboard preview, Phase 16 agent). Raw body
    /// is never written here (D-04/D-07).
    /// </summary>
    public string RedactedContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the normalized plaintext before redaction (ADF→text, XHTML→text). Null when
    /// the connector returned an empty or unparseable body.
    /// </summary>
    public string? NormalizedContent { get; set; }

    /// <summary>Gets or sets the correlation ID linking this ingestion run to connector logs (max 64).</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the UTC timestamp the ingestion completed.</summary>
    public DateTimeOffset IngestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the identity of the user or service that triggered ingestion (max 256).</summary>
    public string IngestedBy { get; set; } = string.Empty;
}
