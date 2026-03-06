using Muonroi.Governance.Policy;

namespace Muonroi.Governance.ControlPlane;

public enum MManagedLicenseStatus
{
    Active = 0,
    Revoked = 1
}

public enum MPolicyBundleStatus
{
    Draft = 0,
    Approved = 1,
    Activated = 2,
    Superseded = 3,
    RolledBack = 4
}

public sealed class MControlPlaneRegistry
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<MControlPlaneLicenseRecord> Licenses { get; set; } = [];
    public List<MControlPlanePolicyBundleRecord> PolicyBundles { get; set; } = [];
    public List<MControlPlaneAuditRecord> AuditTrail { get; set; } = [];
}

public sealed class MControlPlaneLicenseRecord
{
    public string LicenseId { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public LicenseTier Tier { get; set; } = LicenseTier.Licensed;
    public MManagedLicenseStatus Status { get; set; } = MManagedLicenseStatus.Active;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string[] AllowedFeatures { get; set; } = [];
    public string[] TenantAssignments { get; set; } = [];
    public int Revision { get; set; } = 1;
    public string IssuedBy { get; set; } = "control-plane";
    public string LastUpdatedBy { get; set; } = "control-plane";
    public LicensePayload Payload { get; set; } = new();
}

public sealed class MControlPlanePolicyBundleRecord
{
    public string BundleId { get; set; } = string.Empty;
    public string LicenseId { get; set; } = string.Empty;
    public int Version { get; set; }
    public MPolicyBundleStatus Status { get; set; } = MPolicyBundleStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "control-plane";
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? RolledBackAt { get; set; }
    public string? RolledBackBy { get; set; }
    public string? RollbackReason { get; set; }
    public LicensePolicy Policy { get; set; } = new();
}

public sealed class MControlPlaneAuditRecord
{
    public string AuditId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Actor { get; set; } = "control-plane";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string DataHash { get; set; } = string.Empty;
    public string SignatureAlgorithm { get; set; } = string.Empty;
    public string SignatureKeyId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public sealed class MIssueLicenseRequest
{
    public string OrganizationName { get; set; } = string.Empty;
    public LicenseTier Tier { get; set; } = LicenseTier.Licensed;
    public string? ProjectId { get; set; }
    public string[]? AllowedFeatures { get; set; }
    public string[]? TenantAssignments { get; set; }
    public DateTimeOffset? NotBefore { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Fingerprint { get; set; }
    public string? HardwareId { get; set; }
    public string? ServerNonce { get; set; }
    public string? RequestedBy { get; set; }
}

public sealed class MIssueLicenseResult
{
    public required MControlPlaneLicenseRecord License { get; init; }
    public required LicensePayload Payload { get; init; }
}

public sealed class MRevokeLicenseRequest
{
    public string LicenseId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? RequestedBy { get; set; }
}

public sealed class MAssignTenantsRequest
{
    public string LicenseId { get; set; } = string.Empty;
    public string[] TenantIds { get; set; } = [];
    public string? RequestedBy { get; set; }
}

public sealed class MCreatePolicyDraftRequest
{
    public string LicenseId { get; set; } = string.Empty;
    public PolicyEnforcementRules Enforcement { get; set; } = new();
    public Dictionary<string, FeatureQuota> FeatureQuotas { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RequestedBy { get; set; }
}

public sealed class MApprovePolicyBundleRequest
{
    public string BundleId { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
}

public sealed class MActivatePolicyBundleRequest
{
    public string BundleId { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
}

public sealed class MRollbackPolicyBundleRequest
{
    public string LicenseId { get; set; } = string.Empty;
    public int TargetVersion { get; set; }
    public string? Reason { get; set; }
    public string? RequestedBy { get; set; }
}


