using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Enterprise.Policy;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Tenancy.Core;

namespace Muonroi.Governance.License;

/// <summary>
/// Enterprise implementation of ILicenseGuardEnhancer.
/// Injects anti-tampering, chain audit, and fail-closed policy enforcement.
/// Requires: Muonroi.Governance.Enterprise package + valid Enterprise license.
/// </summary>
public sealed class EnterpriseLicenseGuardEnhancer(
    LicenseConfigs configs,
    CodeIntegrityVerifier codeIntegrityVerifier,
    AntiTamperDetector tamperDetector,
    PolicyEnforcer? policyEnforcer = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null) : ILicenseGuardEnhancer
{
    private readonly AntiTamperDetector _tamperDetector = tamperDetector;
    private readonly CodeIntegrityVerifier _codeIntegrityVerifier = codeIntegrityVerifier;
    private readonly ISystemExecutionContextAccessor? _executionContextAccessor = executionContextAccessor;
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastAntiTamperChecks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes the On Startup operation.
    /// </summary>
    public void OnStartup(LicenseConfigs startupConfigs, LicenseState state)
    {
        LicenseEnforcementMode mode = startupConfigs.GetEffectiveEnforcementMode(state.Tier);
        if (mode == LicenseEnforcementMode.Production && state.Tier != LicenseTier.Free)
        {
            _ = _codeIntegrityVerifier.VerifyIntegrity(state, throwOnFailure: GetEffectiveFailMode() != LicenseFailMode.Soft);
        }

        if (startupConfigs.EnableAntiTampering && mode == LicenseEnforcementMode.Production)
        {
            PerformHardenedIntegrityCheck(state);
        }
    }

    /// <summary>
    /// Executes the On Ensure Valid operation.
    /// </summary>
    public void OnEnsureValid(string actionType, LicenseState state)
    {
        EnforceEnterpriseFailClosedPolicy(actionType, state);
        TryRunRuntimeAntiTamperingCheck(actionType, state);

        if (actionType.StartsWith("api.", StringComparison.OrdinalIgnoreCase) && policyEnforcer != null)
        {
            if (!policyEnforcer.CheckApiRateLimit())
            {
                throw new InvalidOperationException("[POLICY] API rate limit exceeded.");
            }
        }

        if (actionType.StartsWith("db.", StringComparison.OrdinalIgnoreCase) && policyEnforcer != null)
        {
            if (!policyEnforcer.CheckDbRateLimit())
            {
                throw new InvalidOperationException("[POLICY] DB rate limit exceeded.");
            }
        }
    }

    /// <summary>
    /// Executes the On Record Action operation.
    /// </summary>
    public void OnRecordAction(LicenseActionContext context, LicenseState state)
    {
        // Hook for enterprise-specific audit side effects.
        _ = context;
        _ = state;
    }

    private LicenseFailMode GetEffectiveFailMode()
    {
        if (policyEnforcer?.CurrentPolicy != null)
        {
            return policyEnforcer.CurrentPolicy.Enforcement.FailMode;
        }

        return configs.FailMode;
    }

    private bool GetEffectiveEnableAntiTampering()
    {
        if (policyEnforcer?.CurrentPolicy != null)
        {
            return policyEnforcer.CurrentPolicy.Enforcement.EnableAntiTampering;
        }

        return configs.EnableAntiTampering;
    }

    private void PerformHardenedIntegrityCheck(LicenseState state)
    {
        if (configs.GetEffectiveEnforcementMode(state.Tier) != LicenseEnforcementMode.Production)
        {
            return;
        }

        string? startupTenantId = ResolveTenantId();
        RunAntiTamperingCheck("startup", startupTenantId, "license.guard.startup", force: true);
    }

    private void TryRunRuntimeAntiTamperingCheck(string actionType, LicenseState state)
    {
        if (configs.GetEffectiveEnforcementMode(state.Tier) != LicenseEnforcementMode.Production)
        {
            return;
        }

        if (!GetEffectiveEnableAntiTampering())
        {
            return;
        }

        string? tenantId = ResolveTenantId();
        string tenantPartition = AntiTamperingTenantPartition.Normalize(tenantId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(0, configs.AntiTamperingCheckIntervalSeconds));
        if (!ShouldRunAntiTamperingCheck(tenantPartition, now, interval))
        {
            return;
        }

        RunAntiTamperingCheck("runtime", tenantId, actionType, force: false);
    }

    private static bool ShouldRunAntiTamperingCheck(string tenantPartition, DateTimeOffset now, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            LastAntiTamperChecks[tenantPartition] = now;
            return true;
        }

        while (true)
        {
            if (!LastAntiTamperChecks.TryGetValue(tenantPartition, out DateTimeOffset lastRun))
            {
                if (LastAntiTamperChecks.TryAdd(tenantPartition, now))
                {
                    return true;
                }

                continue;
            }

            if (now - lastRun < interval)
            {
                return false;
            }

            if (LastAntiTamperChecks.TryUpdate(tenantPartition, now, lastRun))
            {
                return true;
            }
        }
    }

    private void RunAntiTamperingCheck(string scope, string? tenantId, string actionType, bool force)
    {
        string status = "ok";
        bool tamperDetected = false;
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = AntiTamperingRuntimeTelemetry.ActivitySource.StartActivity(
            "anti-tampering.check",
            ActivityKind.Internal);
        activity?.SetTag("antitamper.scope", scope);
        activity?.SetTag("antitamper.action_type", actionType);
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);

        try
        {
            tamperDetected = _tamperDetector.DetectTampering();
            if (tamperDetected)
            {
                status = "tamper_detected";
                activity?.SetStatus(ActivityStatusCode.Error, "Anti-tampering detector signaled possible tampering.");

                if (GetEffectiveFailMode() != LicenseFailMode.Soft)
                {
                    throw new SecurityException("[SEC_TAMPER] Security violation detected via anti-tamper sensors.");
                }

                return;
            }

            if (force || GetEffectiveFailMode() != LicenseFailMode.Soft)
            {
                if (AntiTamperDetector.IsMethodHooked(typeof(LicenseGuard).GetMethod(nameof(LicenseGuard.RecordAction))!))
                {
                    throw new SecurityException("[SEC_TAMPER] Execution integrity compromised.");
                }
            }
        }
        catch (Exception ex)
        {
            if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                status = "error";
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            AntiTamperingRuntimeTelemetry.TrackCheck(scope, status, tenantId, tamperDetected, sw.Elapsed);
        }
    }

    private string? ResolveTenantId()
    {
        string? tenantId = _executionContextAccessor?.Get()?.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = TenantContext.CurrentTenantId;
        }

        return tenantId;
    }

    private void EnforceEnterpriseFailClosedPolicy(string requestedFeature, LicenseState state)
    {
        if (!MEnterpriseSecurityProfile.IsSecureDefaultsActive(configs, state))
        {
            return;
        }

        if (configs.Enterprise.AllowPolicyBypassInProduction)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(requestedFeature))
        {
            return;
        }

        if (policyEnforcer?.CurrentPolicy != null)
        {
            return;
        }

        if (MEnterpriseFailClosedMatrix.ShouldBlock(requestedFeature, MEnterpriseFailureReason.MissingSignedPolicy))
        {
            throw new SecurityException(
                $"[SEC_FAIL_CLOSED] Enterprise Production requires a valid signed policy for '{requestedFeature}'.");
        }
    }
}
