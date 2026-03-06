namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AntiTamperingLicenseGuardTests
{
    [Fact]
    public void Constructor_LicensedWithoutAntiTamperingFeature_Throws()
    {
        LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.MessageBus);
        LicenseConfigs configs = CreateConfigs();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateGuard(configs, state));
        Assert.Contains("anti-tampering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_LicensedWithAntiTamperingFeature_Succeeds()
    {
        LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.AntiTampering);
        LicenseConfigs configs = CreateConfigs();

        LicenseGuard guard = CreateGuard(configs, state);

        Assert.NotNull(guard);
    }

    [Fact]
    public void Constructor_DevelopmentModeWithoutAntiTamperingFeature_Throws()
    {
        LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.MessageBus);
        LicenseConfigs configs = CreateConfigs(enforcementMode: LicenseEnforcementMode.Development);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateGuard(configs, state));
        Assert.Contains("anti-tampering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_EnterpriseWithWildcardFeature_Succeeds()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload()
        };
        state.Payload.LicenseId = "ENT-1";
        state.Payload.AllowedFeatures = ["*"];
        LicenseConfigs configs = CreateConfigs();
        configs.Enterprise = new MEnterpriseSecurityConfigs
        {
            EnableSecureDefaults = true,
            AllowPolicyBypassInProduction = true
        };

        LicenseGuard guard = CreateGuard(configs, state);

        Assert.NotNull(guard);
    }

    [Fact]
    public void Constructor_ProductionMode_EmitsStartupActivity()
    {
        Activity? stopped = null;
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == AntiTamperingRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => stopped = activity;
        ActivitySource.AddActivityListener(listener);

        _ = CreateGuard(CreateConfigs(),
            CreateLicensedState(FreeTierFeatures.Premium.AntiTampering));

        Assert.NotNull(stopped);
        Assert.Equal("startup", stopped!.GetTagItem("antitamper.scope"));
        Assert.Equal("license.guard.startup", stopped.GetTagItem("antitamper.action_type"));
    }

    [Fact]
    public void Constructor_DevelopmentMode_DoesNotEmitStartupActivity()
    {
        Activity? stopped = null;
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == AntiTamperingRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => stopped = activity;
        ActivitySource.AddActivityListener(listener);

        _ = CreateGuard(
            CreateConfigs(enforcementMode: LicenseEnforcementMode.Development),
            CreateLicensedState(FreeTierFeatures.Premium.AntiTampering));

        Assert.Null(stopped);
    }

    [Fact]
    public void EnsureValid_RuntimeCheck_EmitsActivity_WithTenantTag()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        string tenantId = $"tenant-activity-{Guid.NewGuid():N}";
        TenantContext.CurrentTenantId = tenantId;

        try
        {
            Activity? stopped = null;
            using ActivityListener listener = new();
            listener.ShouldListenTo = source => source.Name == AntiTamperingRuntimeTelemetry.ActivitySourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = activity => stopped = activity;
            ActivitySource.AddActivityListener(listener);

            LicenseGuard guard = CreateGuard(CreateConfigs(checkIntervalSeconds: 0),
                CreateLicensedState(FreeTierFeatures.Premium.AntiTampering));
            guard.EnsureValid(FreeTierFeatures.DbQuery);

            Assert.NotNull(stopped);
            Assert.Equal("runtime", stopped!.GetTagItem("antitamper.scope"));
            Assert.Equal(tenantId, stopped.GetTagItem("tenant.id"));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public void EnsureValid_RuntimeCheck_IsolatedByTenantInterval()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        string tenantA = $"tenant-a-{Guid.NewGuid():N}";
        string tenantB = $"tenant-b-{Guid.NewGuid():N}";

        try
        {
            List<string> runtimeTenants = [];
            using ActivityListener listener = new();
            listener.ShouldListenTo = source => source.Name == AntiTamperingRuntimeTelemetry.ActivitySourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = activity =>
            {
                if (!string.Equals(activity.GetTagItem("antitamper.scope")?.ToString(), "runtime",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                runtimeTenants.Add(activity.GetTagItem("tenant.id")?.ToString() ?? string.Empty);
            };
            ActivitySource.AddActivityListener(listener);

            LicenseGuard guard = CreateGuard(CreateConfigs(checkIntervalSeconds: 3600),
                CreateLicensedState(FreeTierFeatures.Premium.AntiTampering));

            TenantContext.CurrentTenantId = tenantA;
            guard.EnsureValid(FreeTierFeatures.DbQuery);
            guard.EnsureValid(FreeTierFeatures.DbQuery);

            TenantContext.CurrentTenantId = tenantB;
            guard.EnsureValid(FreeTierFeatures.DbQuery);

            Assert.Equal(2, runtimeTenants.Count);
            Assert.Contains(tenantA, runtimeTenants);
            Assert.Contains(tenantB, runtimeTenants);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public void EnsureValid_RuntimeCheck_EmitsMetrics_WithTenantTag()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        string tenantId = $"tenant-metrics-{Guid.NewGuid():N}";
        TenantContext.CurrentTenantId = tenantId;

        try
        {
            long checkCount = 0;
            bool sawTenantTag = false;

            using MeterListener listener = new();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AntiTamperingRuntimeTelemetry.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                if (!string.Equals(instrument.Name, "antitamper_checks_total", StringComparison.Ordinal))
                {
                    return;
                }

                checkCount += measurement;
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (string.Equals(tag.Key, "tenant.id", StringComparison.Ordinal) &&
                        string.Equals(tag.Value?.ToString(), tenantId, StringComparison.Ordinal))
                    {
                        sawTenantTag = true;
                    }
                }
            });
            listener.Start();

            LicenseGuard guard = CreateGuard(CreateConfigs(checkIntervalSeconds: 0),
                CreateLicensedState(FreeTierFeatures.Premium.AntiTampering));
            guard.EnsureValid(FreeTierFeatures.DbQuery);

            Assert.True(checkCount > 0);
            Assert.True(sawTenantTag);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    private static LicenseGuard CreateGuard(LicenseConfigs configs, LicenseState state)
    {
        return new LicenseGuard(
            configs,
            state,
            new NoopFingerprintChainStore(),
            new HmacFingerprintSigner(state.Payload, configs));
    }

    private static LicenseConfigs CreateConfigs(
        int checkIntervalSeconds = 30,
        LicenseEnforcementMode enforcementMode = LicenseEnforcementMode.Production)
    {
        LicenseConfigs configs = new()
        {
            EnforcementMode = enforcementMode,
            EnableAntiTampering = true,
            AntiTamperingCheckIntervalSeconds = checkIntervalSeconds,
            FailMode = LicenseFailMode.Soft,
            ProjectSeed = "1234567890123456",
            FingerprintSalt = "tests"
        };
        return configs;
    }

    private static LicenseState CreateLicensedState(params string[] features)
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = features,
            Payload = new LicensePayload()
        };
        state.Payload.LicenseId = "LIC-ANTITAMPER-001";
        state.Payload.AllowedFeatures = features;
        return state;
    }
}
