using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Governance.Abstractions.Integrity;
using Muonroi.Governance.License;

namespace Muonroi.Governance.Abstractions.Tests;

public class ActivationProofTests
{
    [Fact]
    public void GetSigningData_ShouldSortAssemblyManifestAndIncludeNonce()
    {
        ActivationProof proof = new()
        {
            ProofId = "proof",
            LicenseId = "license",
            OrganizationName = "Muonroi",
            Tier = LicenseTier.Enterprise,
            ActivatedAt = DateTimeOffset.Parse("2026-03-23T00:00:00Z"),
            ExpiresAt = DateTimeOffset.Parse("2027-03-23T00:00:00Z"),
            ActivatedEnvironment = "prod",
            MaxSeats = 100,
            Features = ["audit.remote", "audit.trail"],
            HeartbeatNonce = "nonce-123",
            AllowedAssemblyHashes =
            [
                new AssemblyManifestEntry { AssemblyName = "B", Version = "2.0.0", Sha256Hash = "bbb", PublicKeyToken = "2" },
                new AssemblyManifestEntry { AssemblyName = "A", Version = "1.0.0", Sha256Hash = "aaa", PublicKeyToken = "1" }
            ]
        };

        string signingData = proof.GetSigningData();

        Assert.Contains("A:1.0.0:aaa:1;B:2.0.0:bbb:2", signingData);
        Assert.EndsWith("|nonce-123", signingData);
    }
}

public class LicenseRuntimeStatusTests
{
    [Fact]
    public void EvaluateGracePeriod_AfterGraceExpires_ShouldDowngradeToFree()
    {
        LicenseRuntimeStatus status = new();
        status.StartRevocationGrace(DateTimeOffset.UtcNow.AddMinutes(-1));

        bool degraded = status.EvaluateGracePeriod(DateTimeOffset.UtcNow);

        Assert.True(degraded);
        Assert.True(status.IsDegradedToFree);
        Assert.Equal("revoked", status.DegradeReason);
    }

    [Fact]
    public void HasFeature_WhenDegraded_ShouldAllowOnlyFreeBaseline()
    {
        LicenseRuntimeStatus status = new();
        status.DowngradeToFree("manual");

        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload { AllowedFeatures = ["*"] }
        };

        Assert.True(status.HasFeature(state, FreeTierFeatures.HttpRequest));
        Assert.False(status.HasFeature(state, FreeTierFeatures.Premium.AuditTrail));
    }
}

public class ProofTierAccessorTests
{
    [Fact]
    public void RequireMinimumTier_WhenTierIsTooLow_ShouldThrow()
    {
        ProofTierAccessor accessor = new(
            new LicenseState { IsValid = true, Tier = LicenseTier.Free },
            new LicenseRuntimeStatus());

        Assert.Throws<LicenseException>(() => accessor.RequireMinimumTier(LicenseTier.Licensed, "advanced-auth"));
    }
}

public class CommercialLicenseStartupGuardsTests
{
    [Fact]
    public void RequireMinimumTierFromProof_WhenLicenseStateMissing_ShouldThrowOnResolution()
    {
        ServiceCollection services = new();

        services.RequireMinimumTierFromProof(LicenseTier.Enterprise, "audit.remote");
        ServiceProvider provider = services.BuildServiceProvider();

        LicenseException exception = Assert.Throws<LicenseException>(() => provider.GetRequiredService<IHostedService>());
        Assert.Contains("LicenseState is not registered", exception.Message);
    }

    [Fact]
    public void RequireMinimumTierFromProof_WhenDependenciesPresent_ShouldRegisterHostedService()
    {
        ServiceCollection services = new();
        services.AddSingleton(new LicenseState { IsValid = true, Tier = LicenseTier.Enterprise });
        services.AddSingleton(new LicenseRuntimeStatus());

        services.RequireMinimumTierFromProof(LicenseTier.Licensed, "audit.remote");
        ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHostedService>());
    }
}
