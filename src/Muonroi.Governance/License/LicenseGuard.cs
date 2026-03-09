namespace Muonroi.Governance.License;

public sealed class LicenseGuard : ILicenseGuard
{
    private readonly LicenseConfigs _configs;
    private readonly IFingerprintChainStore _chainStore;
    private readonly IFingerprintSigner _signer;
    private readonly ILicenseGuardEnhancer _enhancer;
    private readonly LicenseRuntimeStatus _runtimeStatus;

    private static readonly ConcurrentDictionary<string, string> RollingTokens = new(StringComparer.OrdinalIgnoreCase);

    public LicenseGuard(
        LicenseConfigs configs,
        LicenseState state,
        IFingerprintChainStore chainStore,
        IFingerprintSigner signer,
        LicenseRuntimeStatus? runtimeStatus = null,
        ILicenseGuardEnhancer? enhancer = null)
    {
        _configs = configs;
        Current = state;
        _chainStore = chainStore;
        _signer = signer;
        _runtimeStatus = runtimeStatus ?? new LicenseRuntimeStatus();
        _enhancer = enhancer ?? new NoopLicenseGuardEnhancer();

        _runtimeStatus.InitializeFromProof(state.ActivationProof);
        _ = configs.GetEffectiveEnforcementMode(Tier);
        _enhancer.OnStartup(configs, state);
    }

    public LicenseState Current { get; }

    public LicenseTier Tier => _runtimeStatus.GetEffectiveTier(Current);

    public bool IsFreeMode => Tier == LicenseTier.Free;

    public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
        string? correlationId = null)
    {
        _runtimeStatus.EvaluateGracePeriod(DateTimeOffset.UtcNow);

        if (!Current.IsValid)
        {
            HandleInvalidLicense();
        }

        if (!HasFeature(actionType))
        {
            throw new InvalidOperationException(
                $"[LICENSE] Feature '{actionType}' is not included in your license. Tier: {Tier}.");
        }

        if (IsFreeMode)
        {
            return;
        }

        _enhancer.OnEnsureValid(actionType, Current);

        if (_configs.EnableChain)
        {
            string tenantPartition = ResolveTenantPartition();
            string rollingToken = GetRollingToken(tenantPartition);
            string? lastRecorded = _chainStore.GetLastSignature(tenantPartition);
            if (lastRecorded != null && rollingToken != "INITIAL_BOOT" && rollingToken != lastRecorded)
            {
                HandleChainTampered();
            }
        }
    }

    public bool HasFeature(string featureName)
    {
        return _runtimeStatus.HasFeature(Current, featureName);
    }

    public void EnsureFeature(string featureName)
    {
        if (!HasFeature(featureName))
        {
            throw new InvalidOperationException(
                $"[LICENSE] Feature '{featureName}' is not available under your current license. Tier: {Tier}.");
        }
    }

    public void RecordAction(LicenseActionContext context)
    {
        if (!_configs.EnableChain)
        {
            return;
        }

        EnsureFeature(FreeTierFeatures.Premium.AuditTrail);
        if (!Current.IsValid)
        {
            HandleInvalidLicense();
        }

        if (LicenseExecutionContext.IsInLicenseCheck)
        {
            return;
        }

        using (LicenseExecutionContext.BeginScope())
        {
            string? tenantId = ResolveTenantId(context);
            context.TenantId = tenantId;

            string tenantPartition = NormalizeTenantPartition(tenantId);
            string previous = _chainStore.GetLastSignature(tenantPartition) ?? "GENESIS";
            long nextSeq = _chainStore.GetLastSequence(tenantPartition) + 1;
            string signature = _signer.ComputeSignature(previous, context, nextSeq);

            FingerprintChainEntry entry = new()
            {
                Sequence = nextSeq,
                Timestamp = context.Timestamp,
                TenantId = tenantPartition,
                ActionType = context.ActionType,
                ActionName = context.ActionName,
                PayloadHash = context.PayloadHash,
                PreviousSignature = previous,
                Signature = signature
            };

            _chainStore.Append(entry);
            SetRollingToken(tenantPartition, signature);
            _enhancer.OnRecordAction(context, Current);
        }
    }

    public string GetChainToken()
    {
        return GetRollingToken(ResolveTenantPartition());
    }

    public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
    {
        string projectSeed = _configs.ProjectSeed ?? "DEFAULT";
        string rollingToken = GetRollingToken(ResolveTenantPartition());
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(projectSeed));
        string key = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{rollingToken}:{purpose}")));
        return decryptor(key, encryptedData);
    }

    [Obsolete("Use DecryptSecurely")]
    public string GetFunctionalKey(string purpose)
    {
        return "POISONED_" + Guid.NewGuid();
    }

    private void HandleInvalidLicense()
    {
        if (_configs.FailMode == LicenseFailMode.Soft)
        {
            return;
        }

        throw new InvalidOperationException("[SEC_ERR_01] License validation failed.");
    }

    private void HandleChainTampered()
    {
        if (_configs.FailMode == LicenseFailMode.Soft)
        {
            return;
        }

        throw new InvalidOperationException("[SEC_ERR_02] Security chain integrity check failed.");
    }

    private static string? ResolveTenantId(LicenseActionContext? context = null)
    {
        if (!string.IsNullOrWhiteSpace(context?.TenantId))
        {
            return context.TenantId;
        }

        return TenantContext.CurrentTenantId;
    }

    private static string NormalizeTenantPartition(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "__host__" : tenantId.Trim();
    }

    private static string ResolveTenantPartition()
    {
        return NormalizeTenantPartition(TenantContext.CurrentTenantId);
    }

    private static string GetRollingToken(string tenantPartition)
    {
        return RollingTokens.TryGetValue(tenantPartition, out string? token) ? token : "INITIAL_BOOT";
    }

    private static void SetRollingToken(string tenantPartition, string signature)
    {
        RollingTokens[tenantPartition] = signature;
    }
}
