namespace Muonroi.Governance.Compliance;

/// <summary>
/// Represents the MCompliance Export Source.
/// </summary>
public enum MComplianceExportSource
{
    /// <summary>
    /// Represents the Audit Trail Chain value.
    /// </summary>
    AuditTrailChain = 0,
    /// <summary>
    /// Represents the Control Plane Audit value.
    /// </summary>
    ControlPlaneAudit = 1
}

/// <summary>
/// Represents the MCompliance Export Record.
/// </summary>
public sealed class MComplianceExportRecord
{
    /// <summary>
    /// Gets or sets the Export Sequence.
    /// </summary>
    public long ExportSequence { get; set; }
    /// <summary>
    /// Gets or sets the Occurred At Utc.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Source.
    /// </summary>
    public MComplianceExportSource Source { get; set; }
    /// <summary>
    /// Gets or sets the Event Type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Entity Type.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Entity Id.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Payload Hash.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Previous Hash.
    /// </summary>
    public string PreviousHash { get; set; } = "GENESIS";
    /// <summary>
    /// Gets or sets the Record Hash.
    /// </summary>
    public string RecordHash { get; set; } = string.Empty;
}

/// <summary>
/// Represents the MCompliance Export State.
/// </summary>
public sealed class MComplianceExportState
{
    /// <summary>
    /// Gets or sets the Last Export Sequence.
    /// </summary>
    public long LastExportSequence { get; set; }
    /// <summary>
    /// Gets or sets the Last Record Hash.
    /// </summary>
    public string LastRecordHash { get; set; } = "GENESIS";
    /// <summary>
    /// Gets or sets the Last Exported At Utc.
    /// </summary>
    public DateTimeOffset LastExportedAtUtc { get; set; } = DateTimeOffset.MinValue;
    /// <summary>
    /// Executes the Last Chain Sequence By Tenant operation.
    /// </summary>
    public Dictionary<string, long> LastChainSequenceByTenant { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets or sets the Last Control Plane Audit Cursor.
    /// </summary>
    public string? LastControlPlaneAuditCursor { get; set; }
}

/// <summary>
/// Represents the MCompliance Export Run Result.
/// </summary>
public sealed class MComplianceExportRunResult
{
    /// <summary>
    /// Gets or sets the Executed At Utc.
    /// </summary>
    public DateTimeOffset ExecutedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Is Enabled.
    /// </summary>
    public bool IsEnabled { get; init; }
    /// <summary>
    /// Gets or sets the Exported Count.
    /// </summary>
    public int ExportedCount { get; init; }
    /// <summary>
    /// Gets or sets the Chain Entry Count.
    /// </summary>
    public int ChainEntryCount { get; init; }
    /// <summary>
    /// Gets or sets the Control Plane Audit Count.
    /// </summary>
    public int ControlPlaneAuditCount { get; init; }
    /// <summary>
    /// Gets or sets the Export File Path.
    /// </summary>
    public string? ExportFilePath { get; init; }
    /// <summary>
    /// Gets or sets the Checkpoint File Path.
    /// </summary>
    public string? CheckpointFilePath { get; init; }
    /// <summary>
    /// Gets or sets the Last Record Hash.
    /// </summary>
    public string? LastRecordHash { get; init; }
}

/// <summary>
/// Represents the MCompliance Export Query.
/// </summary>
public sealed class MComplianceExportQuery
{
    /// <summary>
    /// Gets or sets the Start Utc.
    /// </summary>
    public DateTimeOffset? StartUtc { get; set; }
    /// <summary>
    /// Gets or sets the End Utc.
    /// </summary>
    public DateTimeOffset? EndUtc { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Source.
    /// </summary>
    public MComplianceExportSource? Source { get; set; }
    /// <summary>
    /// Gets or sets the Max Records.
    /// </summary>
    public int? MaxRecords { get; set; }
}

/// <summary>
/// Represents the MCompliance Verification Request.
/// </summary>
public sealed class MComplianceVerificationRequest
{
    /// <summary>
    /// Gets or sets the Start Utc.
    /// </summary>
    public DateTimeOffset? StartUtc { get; set; }
    /// <summary>
    /// Gets or sets the End Utc.
    /// </summary>
    public DateTimeOffset? EndUtc { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Source.
    /// </summary>
    public MComplianceExportSource? Source { get; set; }
}

/// <summary>
/// Represents the MCompliance Verification Result.
/// </summary>
public sealed class MComplianceVerificationResult
{
    /// <summary>
    /// Gets or sets the Is Valid.
    /// </summary>
    public bool IsValid { get; init; }
    /// <summary>
    /// Gets or sets the Checked Count.
    /// </summary>
    public int CheckedCount { get; init; }
    /// <summary>
    /// Gets or sets the First Invalid Sequence.
    /// </summary>
    public long? FirstInvalidSequence { get; init; }
    /// <summary>
    /// Gets or sets the Error.
    /// </summary>
    public string? Error { get; init; }
    /// <summary>
    /// Gets or sets the Last Computed Hash.
    /// </summary>
    public string LastComputedHash { get; init; } = "GENESIS";
}

/// <summary>
/// Represents the MCompliance Evidence Pack Request.
/// </summary>
public sealed class MComplianceEvidencePackRequest
{
    /// <summary>
    /// Gets or sets the Start Utc.
    /// </summary>
    public DateTimeOffset? StartUtc { get; set; }
    /// <summary>
    /// Gets or sets the End Utc.
    /// </summary>
    public DateTimeOffset? EndUtc { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Source.
    /// </summary>
    public MComplianceExportSource? Source { get; set; }
    /// <summary>
    /// Gets or sets the Max Records.
    /// </summary>
    public int? MaxRecords { get; set; }
    /// <summary>
    /// Gets or sets the Include Records.
    /// </summary>
    public bool IncludeRecords { get; set; } = true;
    /// <summary>
    /// Gets or sets the Output Path.
    /// </summary>
    public string? OutputPath { get; set; }
}

/// <summary>
/// Represents the MCompliance Evidence Pack Summary.
/// </summary>
public sealed class MComplianceEvidencePackSummary
{
    /// <summary>
    /// Gets or sets the Total Records.
    /// </summary>
    public int TotalRecords { get; set; }
    /// <summary>
    /// Executes the Source Counts operation.
    /// </summary>
    public Dictionary<string, int> SourceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Executes the Event Type Counts operation.
    /// </summary>
    public Dictionary<string, int> EventTypeCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents the MCompliance Evidence Pack Document.
/// </summary>
public sealed class MComplianceEvidencePackDocument
{
    /// <summary>
    /// Gets or sets the Pack Id.
    /// </summary>
    public string PackId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Generated At Utc.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Executes the Filters operation.
    /// </summary>
    public MComplianceExportQuery Filters { get; set; } = new();
    /// <summary>
    /// Executes the Summary operation.
    /// </summary>
    public MComplianceEvidencePackSummary Summary { get; set; } = new();
    /// <summary>
    /// Executes the Verification operation.
    /// </summary>
    public MComplianceVerificationResult Verification { get; set; } = new();
    /// <summary>
    /// Gets or sets the Root Hash.
    /// </summary>
    public string RootHash { get; set; } = "GENESIS";
    /// <summary>
    /// Gets or sets the Pack Hash.
    /// </summary>
    public string PackHash { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Signature Algorithm.
    /// </summary>
    public string SignatureAlgorithm { get; set; } = "HMACSHA256";
    /// <summary>
    /// Gets or sets the identifier of the key that produced <see cref="Signature"/>.
    /// Empty for the local HMAC path; the signer's KeyId for the RSA chain-of-custody path.
    /// </summary>
    public string SigningKeyId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Signature.
    /// </summary>
    public string Signature { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Records.
    /// </summary>
    public List<MComplianceExportRecord>? Records { get; set; }
}

/// <summary>
/// Outcome of verifying a persisted evidence pack's authenticity and integrity.
/// </summary>
public sealed class MComplianceEvidencePackVerifyResult
{
    /// <summary>Whether the stored signature is valid over the stored pack hash.</summary>
    public bool SignatureValid { get; init; }
    /// <summary>
    /// Whether the recomputed content hash matches the stored pack hash.
    /// <see langword="null"/> when the pack does not embed records (cannot fully recompute).
    /// </summary>
    public bool? ContentHashValid { get; init; }
    /// <summary>The signature algorithm declared by the pack (e.g. HMACSHA256, RSA-SHA256).</summary>
    public string SignatureAlgorithm { get; init; } = string.Empty;
    /// <summary>Human-readable detail, populated when verification fails.</summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>True only when the signature is valid and content hash is valid-or-not-applicable.</summary>
    public bool IsTrustworthy => SignatureValid && ContentHashValid != false;
}

/// <summary>
/// Represents the MCompliance Evidence Pack Result.
/// </summary>
public sealed class MComplianceEvidencePackResult
{
    /// <summary>
    /// Gets or sets the Output Path.
    /// </summary>
    public required string OutputPath { get; init; }
    /// <summary>
    /// Gets or sets the Pack.
    /// </summary>
    public required MComplianceEvidencePackDocument Pack { get; init; }
}
