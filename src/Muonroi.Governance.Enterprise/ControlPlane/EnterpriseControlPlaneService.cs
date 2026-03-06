using Muonroi.Governance.Policy;

namespace Muonroi.Governance.ControlPlane;

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

    private readonly IMControlPlaneStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IMControlPlaneSigner _signer = signer ?? throw new ArgumentNullException(nameof(signer));

    public MIssueLicenseResult IssueLicense(MIssueLicenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            throw new ArgumentException("OrganizationName is required.", nameof(request));
        }

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

    public MControlPlaneLicenseRecord RevokeLicense(MRevokeLicenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.LicenseId))
        {
            throw new ArgumentException(LicenseIdMessage, nameof(request));
        }

        string actor = NormalizeActor(request.RequestedBy);
        string reason = string.IsNullOrWhiteSpace(request.Reason) ? "Revoked by control-plane operator" : request.Reason.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? record = FindLicense(registry, request.LicenseId)
                ?? throw new InvalidOperationException($"License '{request.LicenseId}' was not found.");
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

    public MControlPlaneLicenseRecord AssignTenants(MAssignTenantsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.LicenseId))
        {
            throw new ArgumentException(LicenseIdMessage, nameof(request));
        }

        string[] normalizedTenants = NormalizeDistinct(request.TenantIds);
        string actor = NormalizeActor(request.RequestedBy);

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? record = FindLicense(registry, request.LicenseId) ?? throw new InvalidOperationException($"License '{request.LicenseId}' was not found.");
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

    public MControlPlanePolicyBundleRecord CreatePolicyDraft(MCreatePolicyDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.LicenseId))
        {
            throw new ArgumentException(LicenseIdMessage, nameof(request));
        }

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? license = FindLicense(registry, request.LicenseId) ?? throw new InvalidOperationException($"License '{request.LicenseId}' was not found.");
            if (license.Status != MManagedLicenseStatus.Active)
            {
                throw new InvalidOperationException($"License '{request.LicenseId}' is not active.");
            }

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

    public MControlPlanePolicyBundleRecord ApprovePolicyBundle(MApprovePolicyBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.BundleId))
        {
            throw new ArgumentException("BundleId is required.", nameof(request));
        }

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlanePolicyBundleRecord? bundle = FindBundle(registry, request.BundleId) ?? throw new InvalidOperationException($"Policy bundle '{request.BundleId}' was not found.");
            if (bundle.Status != MPolicyBundleStatus.Draft)
            {
                throw new InvalidOperationException(
                    $"Bundle '{request.BundleId}' cannot be approved from status '{bundle.Status}'.");
            }

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

    public MControlPlanePolicyBundleRecord ActivatePolicyBundle(MActivatePolicyBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.BundleId))
        {
            throw new ArgumentException("BundleId is required.", nameof(request));
        }

        string actor = NormalizeActor(request.RequestedBy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlanePolicyBundleRecord? bundle = FindBundle(registry, request.BundleId) ?? throw new InvalidOperationException($"Policy bundle '{request.BundleId}' was not found.");
            if (bundle.Status is not MPolicyBundleStatus.Approved and
                not MPolicyBundleStatus.Activated)
            {
                throw new InvalidOperationException(
                    $"Bundle '{request.BundleId}' cannot be activated from status '{bundle.Status}'.");
            }

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

    public MControlPlanePolicyBundleRecord RollbackPolicyBundle(MRollbackPolicyBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.LicenseId))
        {
            throw new ArgumentException(LicenseIdMessage, nameof(request));
        }

        if (request.TargetVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.TargetVersion), "TargetVersion must be greater than zero.");
        }

        string actor = NormalizeActor(request.RequestedBy);
        string reason = string.IsNullOrWhiteSpace(request.Reason) ? "Rollback requested by operator" : request.Reason.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            MControlPlaneRegistry registry = _store.Load();
            MControlPlaneLicenseRecord? license = FindLicense(registry, request.LicenseId) ?? throw new InvalidOperationException($"License '{request.LicenseId}' was not found.");
            MControlPlanePolicyBundleRecord? currentActive = registry.PolicyBundles
                .Where(x =>
                    x.LicenseId.Equals(request.LicenseId, StringComparison.OrdinalIgnoreCase) &&
                    x.Status == MPolicyBundleStatus.Activated)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault() ?? throw new InvalidOperationException(
                    $"No active policy bundle exists for license '{request.LicenseId}'.");
            MControlPlanePolicyBundleRecord? targetBundle = registry.PolicyBundles
                .Where(x => x.LicenseId.Equals(request.LicenseId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(x => x.Version == request.TargetVersion) ?? throw new InvalidOperationException(
                    $"Target version '{request.TargetVersion}' was not found for license '{request.LicenseId}'.");
            if (targetBundle.BundleId.Equals(currentActive.BundleId, StringComparison.OrdinalIgnoreCase))
            {
                return currentActive;
            }

            if (targetBundle.Status == MPolicyBundleStatus.Draft)
            {
                throw new InvalidOperationException("Rollback target cannot be a draft bundle.");
            }

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

    public bool VerifyLicenseSignature(LicensePayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Signature))
        {
            return false;
        }

        return _signer.Verify(BuildLicenseSigningData(payload), payload.Signature);
    }

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


