namespace Muonroi.Governance.Compliance;

public enum MComplianceExportSource
{
    AuditTrailChain = 0,
    ControlPlaneAudit = 1
}

public sealed class MComplianceExportRecord
{
    public long ExportSequence { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public MComplianceExportSource Source { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string PreviousHash { get; set; } = "GENESIS";
    public string RecordHash { get; set; } = string.Empty;
}

public sealed class MComplianceExportState
{
    public long LastExportSequence { get; set; }
    public string LastRecordHash { get; set; } = "GENESIS";
    public DateTimeOffset LastExportedAtUtc { get; set; } = DateTimeOffset.MinValue;
    public Dictionary<string, long> LastChainSequenceByTenant { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastControlPlaneAuditCursor { get; set; }
}

public sealed class MComplianceExportRunResult
{
    public DateTimeOffset ExecutedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool IsEnabled { get; init; }
    public int ExportedCount { get; init; }
    public int ChainEntryCount { get; init; }
    public int ControlPlaneAuditCount { get; init; }
    public string? ExportFilePath { get; init; }
    public string? CheckpointFilePath { get; init; }
    public string? LastRecordHash { get; init; }
}

public sealed class MComplianceExportQuery
{
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public string? TenantId { get; set; }
    public MComplianceExportSource? Source { get; set; }
    public int? MaxRecords { get; set; }
}

public sealed class MComplianceVerificationRequest
{
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public string? TenantId { get; set; }
    public MComplianceExportSource? Source { get; set; }
}

public sealed class MComplianceVerificationResult
{
    public bool IsValid { get; init; }
    public int CheckedCount { get; init; }
    public long? FirstInvalidSequence { get; init; }
    public string? Error { get; init; }
    public string LastComputedHash { get; init; } = "GENESIS";
}

public sealed class MComplianceEvidencePackRequest
{
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public string? TenantId { get; set; }
    public MComplianceExportSource? Source { get; set; }
    public int? MaxRecords { get; set; }
    public bool IncludeRecords { get; set; } = true;
    public string? OutputPath { get; set; }
}

public sealed class MComplianceEvidencePackSummary
{
    public int TotalRecords { get; set; }
    public Dictionary<string, int> SourceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> EventTypeCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MComplianceEvidencePackDocument
{
    public string PackId { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public MComplianceExportQuery Filters { get; set; } = new();
    public MComplianceEvidencePackSummary Summary { get; set; } = new();
    public MComplianceVerificationResult Verification { get; set; } = new();
    public string RootHash { get; set; } = "GENESIS";
    public string PackHash { get; set; } = string.Empty;
    public string SignatureAlgorithm { get; set; } = "HMACSHA256";
    public string Signature { get; set; } = string.Empty;
    public List<MComplianceExportRecord>? Records { get; set; }
}

public sealed class MComplianceEvidencePackResult
{
    public required string OutputPath { get; init; }
    public required MComplianceEvidencePackDocument Pack { get; init; }
}
