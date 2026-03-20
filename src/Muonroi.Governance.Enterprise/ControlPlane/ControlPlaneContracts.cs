using Muonroi.Governance.Policy;

namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the MManaged License Status.
/// </summary>
public enum MManagedLicenseStatus
{
    /// <summary>
    /// Represents the Active value.
    /// </summary>
    Active = 0,
    /// <summary>
    /// Represents the Revoked value.
    /// </summary>
    Revoked = 1
}

/// <summary>
/// Represents the MPolicy Bundle Status.
/// </summary>
public enum MPolicyBundleStatus
{
    /// <summary>
    /// Represents the Draft value.
    /// </summary>
    Draft = 0,
    /// <summary>
    /// Represents the Approved value.
    /// </summary>
    Approved = 1,
    /// <summary>
    /// Represents the Activated value.
    /// </summary>
    Activated = 2,
    /// <summary>
    /// Represents the Superseded value.
    /// </summary>
    Superseded = 3,
    /// <summary>
    /// Represents the Rolled Back value.
    /// </summary>
    RolledBack = 4
}

/// <summary>
/// Represents the MControl Plane Registry.
/// </summary>
public sealed class MControlPlaneRegistry
{
    /// <summary>
    /// Gets or sets the Schema Version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>
    /// Gets or sets the Created At Utc.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Updated At Utc.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Licenses.
    /// </summary>
    public List<MControlPlaneLicenseRecord> Licenses { get; set; } = [];
    /// <summary>
    /// Gets or sets the Policy Bundles.
    /// </summary>
    public List<MControlPlanePolicyBundleRecord> PolicyBundles { get; set; } = [];
    /// <summary>
    /// Gets or sets the Audit Trail.
    /// </summary>
    public List<MControlPlaneAuditRecord> AuditTrail { get; set; } = [];
}

/// <summary>
/// Represents the MControl Plane License Record.
/// </summary>
public sealed class MControlPlaneLicenseRecord
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the License Key.
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Organization Name.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Tier.
    /// </summary>
    public LicenseTier Tier { get; set; } = LicenseTier.Licensed;
    /// <summary>
    /// Gets or sets the Status.
    /// </summary>
    public MManagedLicenseStatus Status { get; set; } = MManagedLicenseStatus.Active;
    /// <summary>
    /// Gets or sets the Issued At.
    /// </summary>
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Expires At.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the Revoked At.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
    /// <summary>
    /// Gets or sets the Revoked Reason.
    /// </summary>
    public string? RevokedReason { get; set; }
    /// <summary>
    /// Gets or sets the Allowed Features.
    /// </summary>
    public string[] AllowedFeatures { get; set; } = [];
    /// <summary>
    /// Gets or sets the Tenant Assignments.
    /// </summary>
    public string[] TenantAssignments { get; set; } = [];
    /// <summary>
    /// Gets or sets the Revision.
    /// </summary>
    public int Revision { get; set; } = 1;
    /// <summary>
    /// Gets or sets the Issued By.
    /// </summary>
    public string IssuedBy { get; set; } = "control-plane";
    /// <summary>
    /// Gets or sets the Last Updated By.
    /// </summary>
    public string LastUpdatedBy { get; set; } = "control-plane";
    /// <summary>
    /// Executes the Payload operation.
    /// </summary>
    public LicensePayload Payload { get; set; } = new();
}

/// <summary>
/// Represents the MControl Plane Policy Bundle Record.
/// </summary>
public sealed class MControlPlanePolicyBundleRecord
{
    /// <summary>
    /// Gets or sets the Bundle Id.
    /// </summary>
    public string BundleId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Version.
    /// </summary>
    public int Version { get; set; }
    /// <summary>
    /// Gets or sets the Status.
    /// </summary>
    public MPolicyBundleStatus Status { get; set; } = MPolicyBundleStatus.Draft;
    /// <summary>
    /// Gets or sets the Created At.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Created By.
    /// </summary>
    public string CreatedBy { get; set; } = "control-plane";
    /// <summary>
    /// Gets or sets the Approved At.
    /// </summary>
    public DateTimeOffset? ApprovedAt { get; set; }
    /// <summary>
    /// Gets or sets the Approved By.
    /// </summary>
    public string? ApprovedBy { get; set; }
    /// <summary>
    /// Gets or sets the Activated At.
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; set; }
    /// <summary>
    /// Gets or sets the Activated By.
    /// </summary>
    public string? ActivatedBy { get; set; }
    /// <summary>
    /// Gets or sets the Rolled Back At.
    /// </summary>
    public DateTimeOffset? RolledBackAt { get; set; }
    /// <summary>
    /// Gets or sets the Rolled Back By.
    /// </summary>
    public string? RolledBackBy { get; set; }
    /// <summary>
    /// Gets or sets the Rollback Reason.
    /// </summary>
    public string? RollbackReason { get; set; }
    /// <summary>
    /// Executes the Policy operation.
    /// </summary>
    public LicensePolicy Policy { get; set; } = new();
}

/// <summary>
/// Represents the MControl Plane Audit Record.
/// </summary>
public sealed class MControlPlaneAuditRecord
{
    /// <summary>
    /// Gets or sets the Audit Id.
    /// </summary>
    public string AuditId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Event Type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Entity Type.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Entity Id.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Actor.
    /// </summary>
    public string Actor { get; set; } = "control-plane";
    /// <summary>
    /// Gets or sets the Occurred At.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Data Hash.
    /// </summary>
    public string DataHash { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Signature Algorithm.
    /// </summary>
    public string SignatureAlgorithm { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Signature Key Id.
    /// </summary>
    public string SignatureKeyId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Signature.
    /// </summary>
    public string Signature { get; set; } = string.Empty;
}

/// <summary>
/// Represents the MIssue License Request.
/// </summary>
public sealed class MIssueLicenseRequest
{
    /// <summary>
    /// Gets or sets the Organization Name.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Tier.
    /// </summary>
    public LicenseTier Tier { get; set; } = LicenseTier.Licensed;
    /// <summary>
    /// Gets or sets the Project Id.
    /// </summary>
    public string? ProjectId { get; set; }
    /// <summary>
    /// Gets or sets the Allowed Features.
    /// </summary>
    public string[]? AllowedFeatures { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Assignments.
    /// </summary>
    public string[]? TenantAssignments { get; set; }
    /// <summary>
    /// Gets or sets the Not Before.
    /// </summary>
    public DateTimeOffset? NotBefore { get; set; }
    /// <summary>
    /// Gets or sets the Expires At.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the Fingerprint.
    /// </summary>
    public string? Fingerprint { get; set; }
    /// <summary>
    /// Gets or sets the Hardware Id.
    /// </summary>
    public string? HardwareId { get; set; }
    /// <summary>
    /// Gets or sets the Server Nonce.
    /// </summary>
    public string? ServerNonce { get; set; }
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Represents the MIssue License Result.
/// </summary>
public sealed class MIssueLicenseResult
{
    /// <summary>
    /// Gets or sets the License.
    /// </summary>
    public required MControlPlaneLicenseRecord License { get; init; }
    /// <summary>
    /// Gets or sets the Payload.
    /// </summary>
    public required LicensePayload Payload { get; init; }
}

/// <summary>
/// Represents the MRevoke License Request.
/// </summary>
public sealed class MRevokeLicenseRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Reason.
    /// </summary>
    public string? Reason { get; set; }
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Represents the MAssign Tenants Request.
/// </summary>
public sealed class MAssignTenantsRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Tenant Ids.
    /// </summary>
    public string[] TenantIds { get; set; } = [];
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Represents the MCreate Policy Draft Request.
/// </summary>
public sealed class MCreatePolicyDraftRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Executes the Enforcement operation.
    /// </summary>
    public PolicyEnforcementRules Enforcement { get; set; } = new();
    /// <summary>
    /// Executes the Feature Quotas operation.
    /// </summary>
    public Dictionary<string, FeatureQuota> FeatureQuotas { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets or sets the Expires At.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Represents the MApprove Policy Bundle Request.
/// </summary>
public sealed class MApprovePolicyBundleRequest
{
    /// <summary>
    /// Gets or sets the Bundle Id.
    /// </summary>
    public string BundleId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Represents the MActivate Policy Bundle Request.
/// </summary>
public sealed class MActivatePolicyBundleRequest
{
    /// <summary>
    /// Gets or sets the Bundle Id.
    /// </summary>
    public string BundleId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Represents the MRollback Policy Bundle Request.
/// </summary>
public sealed class MRollbackPolicyBundleRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Target Version.
    /// </summary>
    public int TargetVersion { get; set; }
    /// <summary>
    /// Gets or sets the Reason.
    /// </summary>
    public string? Reason { get; set; }
    /// <summary>
    /// Gets or sets the Requested By.
    /// </summary>
    public string? RequestedBy { get; set; }
}


