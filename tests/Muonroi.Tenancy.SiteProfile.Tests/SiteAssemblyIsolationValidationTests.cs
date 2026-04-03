using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Tests;

/// <summary>
/// Tests for ValidateSiteAssemblyDiscovery() in SiteProfileStartupValidator.
/// Verifies that startup validation correctly warns when ISiteProfile implementations
/// from SiteAssemblies are not discovered during registration (per D-15).
/// </summary>
public class SiteAssemblyIsolationValidationTests
{
    // ---------------------------------------------------------------------------
    // Test infrastructure
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal ISiteProfile implementation for test assemblies.
    /// Uses the CURRENT assembly as SiteAssemblies (since we can't create runtime assemblies in tests).
    /// </summary>
    private class DiscoveredProfile : ISiteProfile
    {
        public string SiteId => "discovered-site";
        void ISiteProfile.RegisterServices(IServiceCollection services, IConfiguration configuration) { }
    }

    private class UndiscoveredProfile : ISiteProfile
    {
        public string SiteId => "undiscovered-site";
        void ISiteProfile.RegisterServices(IServiceCollection services, IConfiguration configuration) { }
    }

    private class TestLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }

    private readonly TestLogger<SiteProfileStartupValidator> _logger = new();

    // ---------------------------------------------------------------------------
    // Test 1: SiteAssemblies with all profiles discovered → no [SITE-ASSEMBLY] warning
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SiteAssemblies_AllProfilesDiscovered_NoWarning()
    {
        // Arrange: tracker has "discovered-site" already registered
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("discovered-site");

        // Configure SiteAssemblies with the current test assembly (which contains DiscoveredProfile)
        var options = new SiteProfileOptions
        {
            SiteAssemblies = [typeof(SiteAssemblyIsolationValidationTests).Assembly]
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<SiteProfileOptions>>(
            Microsoft.Extensions.Options.Options.Create(options));

        var sp = services.BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        // Act
        await validator.StartAsync(default);

        // Assert: no [SITE-ASSEMBLY] warnings for DiscoveredProfile (SiteId = "discovered-site" IS in tracker)
        var siteAssemblyWarnings = _logger.Warnings
            .Where(w => w.Contains("[SITE-ASSEMBLY]") && w.Contains("DiscoveredProfile"))
            .ToList();
        siteAssemblyWarnings.Should().BeEmpty(
            "DiscoveredProfile's SiteId 'discovered-site' was registered in the tracker");
    }

    // ---------------------------------------------------------------------------
    // Test 2: SiteAssemblies with undiscovered profile → [SITE-ASSEMBLY] warning logged
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SiteAssemblies_UndiscoveredProfile_LogsWarning()
    {
        // Arrange: tracker does NOT have "undiscovered-site"
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("some-other-site"); // Not undiscovered-site

        var options = new SiteProfileOptions
        {
            SiteAssemblies = [typeof(SiteAssemblyIsolationValidationTests).Assembly]
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<SiteProfileOptions>>(
            Microsoft.Extensions.Options.Options.Create(options));

        var sp = services.BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        // Act
        await validator.StartAsync(default);

        // Assert: [SITE-ASSEMBLY] warning for UndiscoveredProfile
        var warning = _logger.Warnings
            .FirstOrDefault(w => w.Contains("[SITE-ASSEMBLY]") && w.Contains("UndiscoveredProfile"));
        warning.Should().NotBeNull(
            "UndiscoveredProfile's SiteId 'undiscovered-site' was NOT registered in the tracker");
        warning.Should().Contain("undiscovered-site");
    }

    // ---------------------------------------------------------------------------
    // Test 3: No SiteAssemblies configured → no-op (no warnings from this validation)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task NoSiteAssemblies_Configured_IsNoop()
    {
        // Arrange: no SiteAssemblies in options
        var tracker = new SiteProfileRegistrationTracker();
        // No site IDs needed — validation should skip entirely

        var options = new SiteProfileOptions
        {
            SiteAssemblies = null // No external assemblies configured
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<SiteProfileOptions>>(
            Microsoft.Extensions.Options.Options.Create(options));

        var sp = services.BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        // Act
        await validator.StartAsync(default);

        // Assert: no [SITE-ASSEMBLY] warnings at all
        var siteAssemblyWarnings = _logger.Warnings
            .Where(w => w.Contains("[SITE-ASSEMBLY]"))
            .ToList();
        siteAssemblyWarnings.Should().BeEmpty(
            "null SiteAssemblies means no external assembly validation");
    }
}
