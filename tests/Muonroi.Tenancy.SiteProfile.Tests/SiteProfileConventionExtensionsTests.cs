using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Tests;

/// <summary>
/// Tests for AddSiteConventionServices&lt;TServiceInterface&gt; — convention-based keyed registration.
///
/// Real-world model: each site lives in its own assembly with exactly ONE ISiteProfile and
/// one TServiceInterface implementation. Tests simulate this via nested namespaces and
/// single-profile assemblies (the test assembly itself contains many, so we test single-impl scenarios).
/// </summary>
public class SiteProfileConventionExtensionsTests
{
    // ─── Test service interfaces ────────────────────────────────────────────────

    private interface IConventionOrderService { }
    private interface IConventionReportService { }

    // ─── Test ISiteProfile implementations ────────────────────────────────────

    private class AlphaSiteConvProfile : ISiteProfile
    {
        public string SiteId => "ALPHA";
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
    }

    private class BravoSiteConvProfile : ISiteProfile
    {
        public string SiteId => "BRAVO";
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
    }

    // ─── Test service implementations ──────────────────────────────────────────

    // NOTE: both impls exist in the same namespace in the test assembly.
    // Convention ties first impl found to each ISiteProfile.
    private class AlphaConvOrderService : IConventionOrderService { }
    private class BravoConvOrderService : IConventionOrderService { }

    private readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

    [Fact]
    public void AddSiteConventionServices_SingleProfileAssembly_RegistersKeyedService()
    {
        // Arrange: use a dedicated single-profile assembly built as a dynamic assembly sim
        // Since we cannot create separate assemblies, we exercise the API with a helper sub-assembly.
        // Test: pass the test assembly — it has multiple ISiteProfile impls + multiple IConventionOrderService impls.
        // Result: each ISiteProfile gets paired with some IConventionOrderService impl (keyed by SiteId).
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "ALPHA",
        [
            new AlphaSiteConvProfile(),
            new BravoSiteConvProfile()
        ]);

        // Act: convention scan — test assembly has multiple profiles + multiple impls
        services.AddSiteConventionServices<IConventionOrderService>(typeof(SiteProfileConventionExtensionsTests).Assembly);

        var sp = services.BuildServiceProvider();

        // Assert: both sites got keyed registrations (concrete type doesn't matter here — convention assigns one)
        var alphaService = sp.GetKeyedService<IConventionOrderService>("ALPHA");
        var bravoService = sp.GetKeyedService<IConventionOrderService>("BRAVO");

        alphaService.Should().NotBeNull("ALPHA site must have a keyed IConventionOrderService");
        bravoService.Should().NotBeNull("BRAVO site must have a keyed IConventionOrderService");
    }

    [Fact]
    public void AddSiteConventionServices_SiteWithNoImpl_SkipsSilently_NoError()
    {
        // IConventionReportService has NO implementations in this assembly — all sites silently skipped
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "ALPHA",
        [
            new AlphaSiteConvProfile(),
            new BravoSiteConvProfile()
        ]);

        // Act: IConventionReportService has no concrete impls — must not throw (D-08)
        var act = () => services.AddSiteConventionServices<IConventionReportService>(typeof(SiteProfileConventionExtensionsTests).Assembly);

        act.Should().NotThrow("Sites with no TServiceInterface impl must be silently skipped (D-08)");

        var sp = services.BuildServiceProvider();

        // No keyed registrations for a missing impl
        sp.GetKeyedService<IConventionReportService>("ALPHA").Should().BeNull();
        sp.GetKeyedService<IConventionReportService>("BRAVO").Should().BeNull();
    }

    [Fact]
    public void AddSiteConventionServices_ExplicitRegistrationWins_NoDuplicate()
    {
        // Arrange: explicit AddKeyedScoped BEFORE convention scan — explicit must win (D-09)
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "ALPHA",
        [
            new AlphaSiteConvProfile()
        ]);

        // Explicit registration for ALPHA with BravoConvOrderService (intentionally different)
        services.AddKeyedScoped<IConventionOrderService, BravoConvOrderService>("ALPHA");

        // Convention scan — must NOT overwrite the explicit registration
        services.AddSiteConventionServices<IConventionOrderService>(typeof(SiteProfileConventionExtensionsTests).Assembly);

        var sp = services.BuildServiceProvider();

        // Explicit BravoConvOrderService must win — convention scan must skip already-registered key
        var alphaService = sp.GetKeyedService<IConventionOrderService>("ALPHA");
        alphaService.Should().NotBeNull("service must resolve");
        alphaService.Should().BeOfType<BravoConvOrderService>(
            "explicit registration must take precedence over convention scan (D-09)");
    }

    [Fact]
    public void AddSiteConventionServices_MultipleAssemblies_DeduplicatesAndRegisters()
    {
        // Arrange: pass the same assembly twice — deduplication expected, no duplicate registrations
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "ALPHA",
        [
            new AlphaSiteConvProfile(),
            new BravoSiteConvProfile()
        ]);

        var testAssembly = typeof(SiteProfileConventionExtensionsTests).Assembly;

        // Act: duplicate assemblies must be deduplicated
        var act = () => services.AddSiteConventionServices<IConventionOrderService>(testAssembly, testAssembly);
        act.Should().NotThrow("duplicate assemblies must be silently deduplicated");

        var sp = services.BuildServiceProvider();

        // Both sites still get keyed registrations (from the single deduped scan)
        sp.GetKeyedService<IConventionOrderService>("ALPHA").Should().NotBeNull();
        sp.GetKeyedService<IConventionOrderService>("BRAVO").Should().NotBeNull();
    }

    [Fact]
    public void AddSiteConventionServices_AssemblyWithoutISiteProfile_IsSkipped()
    {
        // System.Private.CoreLib has no ISiteProfile — must be silently skipped (D-10)
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "ALPHA",
        [
            new AlphaSiteConvProfile()
        ]);

        var systemAssembly = typeof(string).Assembly; // No ISiteProfile in mscorlib

        var act = () => services.AddSiteConventionServices<IConventionOrderService>(systemAssembly);
        act.Should().NotThrow("assembly with no ISiteProfile must be silently skipped");
    }

    [Fact]
    public void AddSiteConventionServices_AddSiteResolvedService_WiredAutomatically()
    {
        // Convention scan must call AddSiteResolvedService<T> so per-request resolution works
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        services.AddMultiSiteProfilesCore(_emptyConfig, _ => "ALPHA",
        [
            new AlphaSiteConvProfile()
        ]);

        services.AddSiteConventionServices<IConventionOrderService>(typeof(SiteProfileConventionExtensionsTests).Assembly);

        var sp = services.BuildServiceProvider();

        // Per-request resolver must work: IConventionOrderService resolves via ISiteProfileResolver
        // (ALPHA has a keyed registration from convention scan)
        var act = () => sp.GetRequiredService<IConventionOrderService>();
        act.Should().NotThrow("AddSiteResolvedService must have been wired by AddSiteConventionServices");
    }
}
