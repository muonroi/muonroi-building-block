using System.Diagnostics;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Tenancy.Core;

namespace Muonroi.Governance.Tests;

[Collection("NonParallel")]
public class AuditTrailLicenseGuardTests
{
    [Fact]
    public void RecordAction_FreeMode_WithEnableChain_ThrowsFeatureError()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-free";
        try
        {
            LicenseConfigs configs = CreateConfigs(enableChain: true);
            LicenseState state = LicenseState.CreateFree();
            LicenseGuard guard = new(
                configs,
                state,
                new NoopFingerprintChainStore(),
                new HmacFingerprintSigner(state.Payload, configs));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                LicenseActionContext context = new()
                {
                    ActionType = "api.request",
                    ActionName = "/api/test"
                };
                guard.RecordAction(context);
            });

            Assert.Contains("audit-trail", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }
    }

    [Fact]
    public void RecordAction_LicensedWithoutAuditFeature_Throws()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-no-audit";
        try
        {
            LicenseConfigs configs = CreateConfigs(enableChain: true);
            LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.MessageBus);
            LicenseGuard guard = new(
                configs,
                state,
                new NoopFingerprintChainStore(),
                new HmacFingerprintSigner(state.Payload, configs));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                LicenseActionContext context = new()
                {
                    ActionType = "api.request",
                    ActionName = "/api/test"
                };
                guard.RecordAction(context);
            });

            Assert.Contains("audit-trail", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }
    }

    [Fact]
    public void RecordAction_TenantPartitionedChain_IsolatedByTenant()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        string path = Path.Combine(Path.GetTempPath(), "muonroi_audit_chain_tests", $"{Guid.NewGuid():N}.log");
        try
        {
            LicenseConfigs configs = CreateConfigs(enableChain: true, chainFilePath: path);
            LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.AuditTrail);
            FileFingerprintChainStore store = new(null, configs, new MJsonSerializeService());
            LicenseGuard guard = new(
                configs,
                state,
                store,
                new HmacFingerprintSigner(state.Payload, configs));

            TenantContext.CurrentTenantId = "tenant-a";
            LicenseActionContext context = new()
            {
                ActionType = "api.request",
                ActionName = "/a"
            };
            guard.RecordAction(context);
            LicenseActionContext actionContext = new()
            {
                ActionType = "api.request",
                ActionName = "/a2"
            };
            guard.RecordAction(actionContext);

            TenantContext.CurrentTenantId = "tenant-b";
            LicenseActionContext licenseActionContext = new()
            {
                ActionType = "api.request",
                ActionName = "/b"
            };
            guard.RecordAction(licenseActionContext);

            Assert.Equal(2, store.GetLastSequence("tenant-a"));
            Assert.Equal(1, store.GetLastSequence("tenant-b"));

            List<FingerprintChainEntry> tenantAEntries = [.. store.GetRecentEntries(10, tenantId: "tenant-a")];
            List<FingerprintChainEntry> tenantBEntries = [.. store.GetRecentEntries(10, tenantId: "tenant-b")];

            Assert.Equal(2, tenantAEntries.Count);
            Assert.Single(tenantBEntries);
            Assert.All(tenantAEntries, e => Assert.Equal("tenant-a", e.TenantId));
            Assert.All(tenantBEntries, e => Assert.Equal("tenant-b", e.TenantId));
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
            SafeDelete(path);
        }
    }

    [Fact]
    public void GetChainToken_IsolatedByTenant()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        string path = Path.Combine(Path.GetTempPath(), "muonroi_audit_chain_tests", $"{Guid.NewGuid():N}.log");
        try
        {
            LicenseConfigs configs = CreateConfigs(enableChain: true, chainFilePath: path);
            LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.AuditTrail);
            FileFingerprintChainStore store = new(null, configs, new MJsonSerializeService());
            LicenseGuard guard = new(
                configs,
                state,
                store,
                new HmacFingerprintSigner(state.Payload, configs));

            TenantContext.CurrentTenantId = "tenant-token-a";
            LicenseActionContext context = new()
            {
                ActionType = "api.request",
                ActionName = "/a"
            };
            guard.RecordAction(context);
            string tokenA = guard.GetChainToken();

            TenantContext.CurrentTenantId = "tenant-token-b";
            LicenseActionContext actionContext = new()
            {
                ActionType = "api.request",
                ActionName = "/b"
            };
            guard.RecordAction(actionContext);
            string tokenB = guard.GetChainToken();

            TenantContext.CurrentTenantId = "tenant-token-a";
            string tokenAAgain = guard.GetChainToken();

            Assert.NotEqual(tokenA, tokenB);
            Assert.Equal(tokenA, tokenAAgain);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
            SafeDelete(path);
        }
    }

    [Fact]
    public void RecordAction_EmitsAuditTrailActivity_WithTenantTag()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-telemetry";
        string path = Path.Combine(Path.GetTempPath(), "muonroi_audit_chain_tests", $"{Guid.NewGuid():N}.log");

        try
        {
            Activity? stopped = null;
            using ActivityListener listener = new();
            listener.ShouldListenTo = source => source.Name == AuditTrailRuntimeTelemetry.ActivitySourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = activity => stopped = activity;
            ActivitySource.AddActivityListener(listener);

            LicenseConfigs configs = CreateConfigs(enableChain: true, chainFilePath: path);
            LicenseState state = CreateLicensedState(FreeTierFeatures.Premium.AuditTrail);
            LicenseGuard guard = new(
                configs,
                state,
                new FileFingerprintChainStore(null, configs, new MJsonSerializeService()),
                new HmacFingerprintSigner(state.Payload, configs));

            LicenseActionContext context = new()
            {
                ActionType = "api.request",
                ActionName = "/audit"
            };
            guard.RecordAction(context);

            Assert.NotNull(stopped);
            string? operation = stopped!.GetTagItem("audittrail.operation")?.ToString();
            Assert.True(operation == "record_action" || operation == "store_append");
            Assert.Equal("tenant-telemetry", stopped.GetTagItem("tenant.id"));
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
            SafeDelete(path);
        }
    }

    private static LicenseConfigs CreateConfigs(bool enableChain, string? chainFilePath = null)
    {
        return new LicenseConfigs
        {
            EnableChain = enableChain,
            ChainStorage = LicenseChainStorage.File,
            ChainFilePath = chainFilePath ?? Path.Combine(Path.GetTempPath(), "muonroi_audit_chain_tests", $"{Guid.NewGuid():N}.log"),
            EnforcementMode = LicenseEnforcementMode.Development,
            ProjectSeed = "1234567890123456",
            FingerprintSalt = "tests"
        };
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
        state.Payload.LicenseId = "LIC-TEST-001";
        state.Payload.AllowedFeatures = features;
        return state;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) &&
                !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}
