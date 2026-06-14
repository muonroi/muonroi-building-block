namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.TraceabilityEntities;

/// <summary>
/// A persisted, tenant-scoped, retriable outbound-sync job (Phase 17 / SYNC-02). On approval
/// (Draft → Active) of an <em>ingested</em> rule, one job row is enqueued capturing the full
/// provenance needed to push the resulting living-doc back to its originating system. A background
/// processor (plan 17-03) claims <see cref="Status"/> = <c>Pending</c> / retriable rows, asserts
/// tenant scope <em>from this row's <see cref="TenantId"/></em> (never request context — background
/// threads have none), pushes via the existing <c>HttpConnector</c>, and updates the row.
/// <para>
/// This is the persistence foundation only — the enqueue path lands in plan 17-02 and the background
/// processor in 17-03. The table is tenant-scoped with RLS ENABLE+FORCE and a <c>tenant_isolation</c>
/// policy carrying BOTH USING and WITH CHECK (T-17-01); a USING-only policy is the forbidden PROD-01
/// cross-tenant-write regression. Failures are surfaced as a retriable <c>Failed</c> state with
/// <see cref="AttemptCount"/> / <see cref="NextRetryUtc"/> — never a swallowed error (SYNC-02).
/// </para>
/// </summary>
public sealed class OutboundSyncJobRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128). The background processor asserts
    /// the RLS scope from this value, never from request context.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name whose activation enqueued this job (max 256).</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>Gets or sets the activated ruleset version this job pushes.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the originating ingested source document id (the SYNC-01 push target
    /// provenance). Always set for an enqueued job — only ingested activations enqueue.</summary>
    public Guid? SourceDocumentId { get; set; }

    /// <summary>Gets or sets the approver identity captured at activation time (max 256).</summary>
    public string Approver { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp the job was enqueued (at activation).</summary>
    public DateTime EnqueuedAtUtc { get; set; }

    /// <summary>Gets or sets the job status: <c>Pending</c> | <c>Succeeded</c> | <c>Failed</c>
    /// (max 32). <c>Failed</c> with a future <see cref="NextRetryUtc"/> is the surfaced retriable
    /// state.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Gets or sets how many push attempts have been made; drives bounded back-off.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Gets or sets the last failure detail — HTTP status + truncated response body written
    /// by the 17-03 processor (no-silent-catch). Null while pending / on success. The no-PII/no-content
    /// constraint is enforced by the writer, not the schema (T-17-04).</summary>
    public string? LastError { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last push attempt; null until first attempt.</summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>Gets or sets the earliest UTC time the job may be retried after a failure; null until
    /// a retry is scheduled.</summary>
    public DateTime? NextRetryUtc { get; set; }
}
