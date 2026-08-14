namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the MEnterprise Control Plane Service.
/// </summary>
public sealed class MEnterpriseControlPlaneService(
    IMControlPlaneStore store,
    IMControlPlaneSigner signer,
    IMDateTimeService dateTimeService,
    IMJsonSerializeService jsonSerializeService) : IMEnterpriseControlPlaneService
{
    private const string LicenseIdMessage = "LicenseId is required.";
    private const string EntityType = "policy-bundle";
    private static readonly string[] LicensedDefaultEntitlements =
    [
        "auth.rbac_plus",
        "tenancy.strict",
        "rules.runtime",
        "transport.grpc",
        "transport.message_bus",
        "cache.distributed",
        "audit.trail",
        "runtime.anti_tampering",
        "audit.remote"
    ];

    private static readonly string[] EnterpriseDefaultEntitlements = ["*"];
    private readonly object _stateLock = new();

    private readonly IMControlPlaneStore _store = MGuard.NotNull(store);
    private readonly IMControlPlaneSigner _signer = MGuard.NotNull(signer);

    /// <summary>
    /// Executes the Issue License operation.
    /// </summary>
    public MIssueLicenseResult IssueLicense(MIssueLicenseRequest request)
    {
        MGuard.NotNull(request);

        MGuard.Against(string.IsNullOrWhiteSpace(request.OrganizationName), "OrganizationName is required.");

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string organization = request.OrganizationName.Trim();

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            string licenseId = GenerateLicenseId();
            string[] tenantAssignments = NormalizeDistinct(request.TenantAssignments);
            string[] allowedFeatures = ResolveAllowedFeatures(request.Tier, request.AllowedFeatures);

            LicensePayload payload = new()
            {
                LicenseId = licenseId,
                ProjectId = request.ProjectId?.Trim(),
                TenantId = tenantAssignments.FirstOrDefault(),
                AllowedFeatures = allowedFeatures,
                Fingerprint = request.Fingerprint?.Trim(),
                HardwareId = request.HardwareId?.Trim(),
                ServerNonce = request.ServerNonce?.Trim(),
                NotBefore = request.NotBefore ?? now,
                ExpiresAt = request.ExpiresAt
            };
            payload.Signature = _signer.Sign(BuildLicenseSigningData(payload));

            MControlPlaneLicenseRecord record = new()
            {
                LicenseId = licenseId,
                LicenseKey = GenerateLicenseKey(request.Tier, organization),
                OrganizationName = organization,
                Tier = request.Tier,
                Status = MManagedLicenseStatus.Active,
                IssuedAt = now,
                ExpiresAt = request.ExpiresAt,
                AllowedFeatures = allowedFeatures,
                TenantAssignments = tenantAssignments,
                Revision = 1,
                IssuedBy = actor,
                LastUpdatedBy = actor,
                Payload = payload
            };

            registry.Licenses.Add(record);
            AppendAudit(
                registry,
                eventType: "license.issued",
                entityType: "license",
                entityId: record.LicenseId,
                actor: actor,
                details: new
                {
                    record.Tier,
                    record.OrganizationName,
                    record.ExpiresAt,
                    record.AllowedFeatures,
                    record.TenantAssignments
                });

            _store.Save(registry);

            return new MIssueLicenseResult
            {
                License = record,
                Payload = payload
            };
        }
    }

    /// <summary>
    /// Executes the Revoke License operation.
    /// </summary>
    public MControlPlaneLicenseRecord RevokeLicense(MRevokeLicenseRequest request)
    {
        MGuard.NotNull(request);
        MGuard.Against(string.IsNullOrWhiteSpace(request.LicenseId), LicenseIdMessage);

        string actor = NormalizeActor(request.RequestedBy);
        string reason = string.IsNullOrWhiteSpace(request.Reason) ? "Revoked by control-plane operator" : request.Reason.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? recordNullable = FindLicense(registry, request.LicenseId);
            MGuard.State(recordNullable is not null, $"License '{request.LicenseId}' was not found.");
            MControlPlaneLicenseRecord record = MGuard.NotNull(recordNullable);
            if (record.Status != MManagedLicenseStatus.Revoked)
            {
                record.Status = MManagedLicenseStatus.Revoked;
                record.RevokedAt = now;
                record.RevokedReason = reason;
                record.Revision += 1;
                record.LastUpdatedBy = actor;
            }

            AppendAudit(
                registry,
                eventType: "license.revoked",
                entityType: "license",
                entityId: record.LicenseId,
                actor: actor,
                details: new
                {
                    Reason = reason,
                    record.RevokedAt
                });

            _store.Save(registry);
            return record;
        }
    }

    /// <summary>
    /// Executes the Assign Tenants operation.
    /// </summary>
    public MControlPlaneLicenseRecord AssignTenants(MAssignTenantsRequest request)
    {
        MGuard.NotNull(request);
        MGuard.Against(string.IsNullOrWhiteSpace(request.LicenseId), LicenseIdMessage);

        string[] normalizedTenants = NormalizeDistinct(request.TenantIds);
        string actor = NormalizeActor(request.RequestedBy);

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? recordNullable = FindLicense(registry, request.LicenseId);
            MGuard.State(recordNullable is not null, $"License '{request.LicenseId}' was not found.");
            MControlPlaneLicenseRecord record = MGuard.NotNull(recordNullable);
            record.TenantAssignments = normalizedTenants;
            if (record.Payload != null)
            {
                record.Payload.TenantId = normalizedTenants.FirstOrDefault();
            }

            record.Revision += 1;
            record.LastUpdatedBy = actor;

            AppendAudit(
                registry,
                eventType: "license.tenants.assigned",
                entityType: "license",
                entityId: record.LicenseId,
                actor: actor,
                details: new
                {
                    TenantCount = normalizedTenants.Length,
                    TenantIds = normalizedTenants
                });

            _store.Save(registry);
            return record;
        }
    }

    /// <summary>
    /// Executes the Create Policy Draft operation.
    /// </summary>
    public MControlPlanePolicyBundleRecord CreatePolicyDraft(MCreatePolicyDraftRequest request)
    {
        MGuard.NotNull(request);
        MGuard.Against(string.IsNullOrWhiteSpace(request.LicenseId), LicenseIdMessage);

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? licenseNullable = FindLicense(registry, request.LicenseId);
            MGuard.State(licenseNullable is not null, $"License '{request.LicenseId}' was not found.");
            MControlPlaneLicenseRecord license = MGuard.NotNull(licenseNullable);
            MGuard.State(license.Status == MManagedLicenseStatus.Active, $"License '{request.LicenseId}' is not active.");

            int nextVersion = registry.PolicyBundles
                .Where(x => x.LicenseId.Equals(request.LicenseId, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Version)
                .DefaultIfEmpty(0)
                .Max() + 1;

            LicensePolicy policy = new()
            {
                PolicyId = $"pol_{request.LicenseId}_{nextVersion:D4}",
                Version = $"1.0.{nextVersion}",
                LicenseId = request.LicenseId.Trim(),
                IssuedAt = now,
                ExpiresAt = request.ExpiresAt ?? license.ExpiresAt,
                Enforcement = CloneEnforcement(request.Enforcement),
                FeatureQuotas = CloneFeatureQuotas(request.FeatureQuotas)
            };

            MControlPlanePolicyBundleRecord bundle = new()
            {
                BundleId = GenerateBundleId(),
                LicenseId = request.LicenseId.Trim(),
                Version = nextVersion,
                Status = MPolicyBundleStatus.Draft,
                CreatedAt = now,
                CreatedBy = actor,
                Policy = policy
            };

            registry.PolicyBundles.Add(bundle);
            AppendAudit(
                registry,
                eventType: "policy.draft.created",
                entityType: EntityType,
                entityId: bundle.BundleId,
                actor: actor,
                details: new
                {
                    bundle.LicenseId,
                    bundle.Version,
                    bundle.Policy.PolicyId
                });

            _store.Save(registry);
            return bundle;
        }
    }

    /// <summary>
    /// Executes the Approve Policy Bundle operation.
    /// </summary>
    public MControlPlanePolicyBundleRecord ApprovePolicyBundle(MApprovePolicyBundleRequest request)
    {
        MGuard.NotNull(request);
        MGuard.Against(string.IsNullOrWhiteSpace(request.BundleId), "BundleId is required.");

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlanePolicyBundleRecord? bundleNullable = FindBundle(registry, request.BundleId);
            MGuard.State(bundleNullable is not null, $"Policy bundle '{request.BundleId}' was not found.");
            MControlPlanePolicyBundleRecord bundle = MGuard.NotNull(bundleNullable);
            MGuard.State(bundle.Status == MPolicyBundleStatus.Draft, $"Bundle '{request.BundleId}' cannot be approved from status '{bundle.Status}'.");

            bundle.Policy.Signature = _signer.Sign(BuildPolicySigningData(bundle.Policy));
            bundle.Status = MPolicyBundleStatus.Approved;
            bundle.ApprovedAt = now;
            bundle.ApprovedBy = actor;

            AppendAudit(
                registry,
                eventType: "policy.approved",
                entityType: EntityType,
                entityId: bundle.BundleId,
                actor: actor,
                details: new
                {
                    bundle.LicenseId,
                    bundle.Version,
                    bundle.Policy.PolicyId
                });

            _store.Save(registry);
            return bundle;
        }
    }

    /// <summary>
    /// Executes the Activate Policy Bundle operation.
    /// </summary>
    public MControlPlanePolicyBundleRecord ActivatePolicyBundle(MActivatePolicyBundleRequest request)
    {
        MGuard.NotNull(request);
        MGuard.Against(string.IsNullOrWhiteSpace(request.BundleId), "BundleId is required.");

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlanePolicyBundleRecord? bundleNullable = FindBundle(registry, request.BundleId);
            MGuard.State(bundleNullable is not null, $"Policy bundle '{request.BundleId}' was not found.");
            MControlPlanePolicyBundleRecord bundle = MGuard.NotNull(bundleNullable);
            MGuard.State(bundle.Status is MPolicyBundleStatus.Approved or MPolicyBundleStatus.Activated, $"Bundle '{request.BundleId}' cannot be activated from status '{bundle.Status}'.");

            if (string.IsNullOrWhiteSpace(bundle.Policy.Signature))
            {
                bundle.Policy.Signature = _signer.Sign(BuildPolicySigningData(bundle.Policy));
            }

            List<MControlPlanePolicyBundleRecord> activeForLicense = [.. registry.PolicyBundles
                .Where(x =>
                    x.LicenseId.Equals(bundle.LicenseId, StringComparison.OrdinalIgnoreCase) &&
                    x.Status == MPolicyBundleStatus.Activated &&
                    !x.BundleId.Equals(bundle.BundleId, StringComparison.OrdinalIgnoreCase))];

            foreach (MControlPlanePolicyBundleRecord? active in activeForLicense)
            {
                active.Status = MPolicyBundleStatus.Superseded;
            }

            bundle.Status = MPolicyBundleStatus.Activated;
            bundle.ActivatedAt = now;
            bundle.ActivatedBy = actor;

            AppendAudit(
                registry,
                eventType: "policy.activated",
                entityType: EntityType,
                entityId: bundle.BundleId,
                actor: actor,
                details: new
                {
                    bundle.LicenseId,
                    bundle.Version,
                    SupersededBundles = activeForLicense.Select(x => x.BundleId).ToArray()
                });

            _store.Save(registry);
            return bundle;
        }
    }

    /// <summary>
    /// Executes the Rollback Policy Bundle operation.
    /// </summary>
    public MControlPlanePolicyBundleRecord RollbackPolicyBundle(MRollbackPolicyBundleRequest request)
    {
        MGuard.NotNull(request);
        MGuard.Against(string.IsNullOrWhiteSpace(request.LicenseId), LicenseIdMessage);

        MGuard.Against(request.TargetVersion <= 0, "TargetVersion must be greater than zero.");

        string actor = NormalizeActor(request.RequestedBy);
        string reason = string.IsNullOrWhiteSpace(request.Reason) ? "Rollback requested by operator" : request.Reason.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? licenseNullable = FindLicense(registry, request.LicenseId);
            MGuard.State(licenseNullable is not null, $"License '{request.LicenseId}' was not found.");
            MControlPlaneLicenseRecord license = MGuard.NotNull(licenseNullable);
            MControlPlanePolicyBundleRecord? currentActiveNullable = registry.PolicyBundles
                .Where(x =>
                    x.LicenseId.Equals(request.LicenseId, StringComparison.OrdinalIgnoreCase) &&
                    x.Status == MPolicyBundleStatus.Activated)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
            MGuard.State(currentActiveNullable is not null, $"No active policy bundle exists for license '{request.LicenseId}'.");
            MControlPlanePolicyBundleRecord currentActive = MGuard.NotNull(currentActiveNullable);
            MControlPlanePolicyBundleRecord? targetBundleNullable = registry.PolicyBundles
                .Where(x => x.LicenseId.Equals(request.LicenseId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(x => x.Version == request.TargetVersion);
            MGuard.State(targetBundleNullable is not null, $"Target version '{request.TargetVersion}' was not found for license '{request.LicenseId}'.");
            MControlPlanePolicyBundleRecord targetBundle = MGuard.NotNull(targetBundleNullable);
            if (targetBundle.BundleId.Equals(currentActive.BundleId, StringComparison.OrdinalIgnoreCase))
            {
                return currentActive;
            }

            MGuard.State(targetBundle.Status != MPolicyBundleStatus.Draft, "Rollback target cannot be a draft bundle.");

            if (string.IsNullOrWhiteSpace(targetBundle.Policy.Signature))
            {
                targetBundle.Policy.Signature = _signer.Sign(BuildPolicySigningData(targetBundle.Policy));
            }

            currentActive.Status = MPolicyBundleStatus.RolledBack;
            currentActive.RolledBackAt = now;
            currentActive.RolledBackBy = actor;
            currentActive.RollbackReason = reason;

            targetBundle.Status = MPolicyBundleStatus.Activated;
            targetBundle.ActivatedAt = now;
            targetBundle.ActivatedBy = actor;

            AppendAudit(
                registry,
                eventType: "policy.rolled-back",
                entityType: EntityType,
                entityId: targetBundle.BundleId,
                actor: actor,
                details: new
                {
                    request.LicenseId,
                    FromVersion = currentActive.Version,
                    ToVersion = targetBundle.Version,
                    Reason = reason
                });

            _store.Save(registry);
            return targetBundle;
        }
    }

    /// <summary>
    /// Executes the Get License operation.
    /// </summary>
    public MControlPlaneLicenseRecord? GetLicense(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId))
        {
            return null;
        }

        lock (_stateLock)
        {
            return FindLicense(_store.Load(), licenseId);
        }
    }

    /// <summary>
    /// Executes the Get Policy Bundles operation.
    /// </summary>
    public IReadOnlyList<MControlPlanePolicyBundleRecord> GetPolicyBundles(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId))
        {
            return [];
        }

        lock (_stateLock)
        {
            return [.. _store.Load()
                .PolicyBundles
                .Where(x => x.LicenseId.Equals(licenseId.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Version)];
        }
    }

    /// <summary>
    /// Executes the Get Active Policy Bundle operation.
    /// </summary>
    public MControlPlanePolicyBundleRecord? GetActivePolicyBundle(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId))
        {
            return null;
        }

        lock (_stateLock)
        {
            return _store.Load()
                .PolicyBundles
                .Where(x =>
                    x.LicenseId.Equals(licenseId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    x.Status == MPolicyBundleStatus.Activated)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Executes the Get Audit Trail operation.
    /// </summary>
    public IReadOnlyList<MControlPlaneAuditRecord> GetAuditTrail(int take = 100)
    {
        if (take <= 0)
        {
            take = 100;
        }

        lock (_stateLock)
        {
            return [.. _store.Load()
                .AuditTrail
                .OrderByDescending(x => x.OccurredAt)
                .Take(take)];
        }
    }

    /// <summary>
    /// Executes the Verify License Signature operation.
    /// </summary>
    public bool VerifyLicenseSignature(LicensePayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Signature))
        {
            return false;
        }

        return _signer.Verify(BuildLicenseSigningData(payload), payload.Signature);
    }

    /// <summary>
    /// Executes the Verify Policy Bundle Signature operation.
    /// </summary>
    public bool VerifyPolicyBundleSignature(MControlPlanePolicyBundleRecord bundle)
    {
        if (bundle == null ||
            bundle.Policy == null ||
            string.IsNullOrWhiteSpace(bundle.Policy.Signature))
        {
            return false;
        }

        return _signer.Verify(BuildPolicySigningData(bundle.Policy), bundle.Policy.Signature);
    }

    /// <summary>
    /// Executes the Verify Audit Record Signature operation.
    /// </summary>
    public bool VerifyAuditRecordSignature(MControlPlaneAuditRecord auditRecord)
    {
        if (auditRecord == null || string.IsNullOrWhiteSpace(auditRecord.Signature))
        {
            return false;
        }

        string payload = BuildAuditSignaturePayload(
            eventType: auditRecord.EventType,
            entityType: auditRecord.EntityType,
            entityId: auditRecord.EntityId,
            actor: auditRecord.Actor,
            occurredAt: auditRecord.OccurredAt,
            dataHash: auditRecord.DataHash);

        return _signer.Verify(payload, auditRecord.Signature);
    }

    private void AppendAudit(
        MControlPlaneRegistry registry,
        string eventType,
        string entityType,
        string entityId,
        string actor,
        object details)
    {
        string detailsJson = jsonSerializeService.Serialize(details);
        string dataHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(detailsJson)));
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        string signaturePayload = BuildAuditSignaturePayload(eventType, entityType, entityId, actor, occurredAt, dataHash);
        string signature = _signer.Sign(signaturePayload);

        MControlPlaneAuditRecord item = new()
        {
            AuditId = $"audit_{Guid.NewGuid().ToString("N")[..12]}",
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Actor = actor,
            OccurredAt = occurredAt,
            DataHash = dataHash,
            SignatureAlgorithm = _signer.SignatureAlgorithm,
            SignatureKeyId = _signer.KeyId,
            Signature = signature
        };
        registry.AuditTrail.Add(item);
    }

    private static MControlPlaneLicenseRecord? FindLicense(MControlPlaneRegistry registry, string licenseId)
    {
        return registry.Licenses
            .FirstOrDefault(x => x.LicenseId.Equals(licenseId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static MControlPlanePolicyBundleRecord? FindBundle(MControlPlaneRegistry registry, string bundleId)
    {
        return registry.PolicyBundles
            .FirstOrDefault(x => x.BundleId.Equals(bundleId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeActor(string? actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "control-plane" : actor.Trim();
    }

    private static string[] NormalizeDistinct(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return [];
        }

        return [.. values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    private static string[] ResolveAllowedFeatures(LicenseTier tier, IEnumerable<string>? requested)
    {
        string[] explicitRequested = NormalizeDistinct(requested);
        if (explicitRequested.Length > 0)
        {
            return explicitRequested;
        }

        return tier switch
        {
            LicenseTier.Enterprise => EnterpriseDefaultEntitlements,
            LicenseTier.Licensed => LicensedDefaultEntitlements,
            _ => FreeTierFeatures.All
        };
    }

    private static string BuildLicenseSigningData(LicensePayload payload)
    {
        var signingData = new
        {
            payload.LicenseId,
            payload.ProjectId,
            payload.TenantId,
            payload.AllowedFeatures,
            payload.Fingerprint,
            payload.HardwareId,
            payload.ServerNonce,
            payload.NotBefore,
            payload.ExpiresAt
        };
        return JsonSerializer.Serialize(signingData); // MBB002-exempt: static helper method — signing data serialization
    }

    private static string BuildPolicySigningData(LicensePolicy policy)
    {
        var signingData = new
        {
            policy.PolicyId,
            policy.Version,
            policy.LicenseId,
            policy.IssuedAt,
            policy.ExpiresAt,
            policy.Enforcement,
            policy.FeatureQuotas
        };
        return JsonSerializer.Serialize(signingData); // MBB002-exempt: static helper method — signing data serialization
    }

    private static string BuildAuditSignaturePayload(
        string eventType,
        string entityType,
        string entityId,
        string actor,
        DateTimeOffset occurredAt,
        string dataHash)
    {
        return $"{eventType}|{entityType}|{entityId}|{actor}|{occurredAt:O}|{dataHash}";
    }

    private static PolicyEnforcementRules CloneEnforcement(PolicyEnforcementRules source)
    {
        PolicyEnforcementRules rules = new()
        {
            EnforceOnDatabase = source.EnforceOnDatabase,
            EnableAntiTampering = source.EnableAntiTampering,
            FailMode = source.FailMode,
            MaxApiRequestsPerMinute = source.MaxApiRequestsPerMinute,
            MaxDbOperationsPerMinute = source.MaxDbOperationsPerMinute
        };
        return rules;
    }

    private static Dictionary<string, FeatureQuota> CloneFeatureQuotas(Dictionary<string, FeatureQuota>? source)
    {
        if (source == null || source.Count == 0)
        {
            return new Dictionary<string, FeatureQuota>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, FeatureQuota> cloned = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, FeatureQuota> kvp in source)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                continue;
            }

            cloned[kvp.Key.Trim()] = new FeatureQuota
            {
                MaxUsagePerDay = kvp.Value.MaxUsagePerDay,
                MaxConcurrentUsage = kvp.Value.MaxConcurrentUsage
            };
        }

        return cloned;
    }

    private static string GenerateLicenseId()
    {
        return $"lic_{Guid.NewGuid().ToString("N")[..12]}";
    }

    private static string GenerateBundleId()
    {
        return $"bundle_{Guid.NewGuid().ToString("N")[..12]}";
    }

    private string GenerateLicenseKey(LicenseTier tier, string organization)
    {
        string prefix = tier switch
        {
            LicenseTier.Enterprise => "ENT",
            LicenseTier.Licensed => "LIC",
            _ => "FREE"
        };

        string dateToken = dateTimeService.UtcNow().ToString("yyyyMMdd");
        return $"{prefix}-{dateToken}-{NewRandomSegment(4)}-{NewRandomSegment(4)}|{organization}";
    }

    private static string NewRandomSegment(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] buffer = new char[length];
        for (int i = 0; i < length; i++)
        {
            buffer[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(buffer);
    }
}


