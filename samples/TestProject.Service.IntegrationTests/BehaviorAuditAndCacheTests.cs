using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Tenancy.SiteProfile.Web.Behaviors;
using TestProject.Service.IntegrationTests.Helpers;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// Integration tests for SiteAuditBehavior (BTEL-01) and SiteCachingBehavior (BTEL-02).
/// These tests verify that DI-level behaviors register the correct keyed services
/// and produce observable signals (log messages, resolved services) without an HTTP pipeline.
/// </summary>
public sealed class BehaviorAuditAndCacheTests
{
    private static IConfiguration EmptyConfig =>
        new ConfigurationBuilder().Build();

    // -------------------------------------------------------------------------
    // BTEL-01 — SiteAuditBehavior startup log tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// BTEL-01: SiteAuditBehavior registers an IHostedService that emits
    /// "SiteId: {SiteId} registered" for each site when StartAsync() is called.
    /// </summary>
    [Fact]
    public async Task SiteAuditBehavior_LogsRegisteredForEachSite_AtStartup()
    {
        // Arrange
        var logCapture = new LogCapture();
        var services = new ServiceCollection();

        // Register logging + IMLog dependencies so IMLog<SiteAuditBehavior> resolves correctly.
        // SystemExecutionContextAccessor is required by MLog<T> constructor.
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b => b.AddMuonroiLogging().AddProvider(logCapture));

        // Apply SiteAuditBehavior for 3 separate sites.
        var config = EmptyConfig;
        new SiteAuditBehavior().Apply(services, config, "DEFAULT");
        new SiteAuditBehavior().Apply(services, config, "ALPHA");
        new SiteAuditBehavior().Apply(services, config, "BRAVO");

        await using var sp = services.BuildServiceProvider();

        // Act — resolve and start all IHostedService instances.
        var hostedServices = sp.GetServices<IHostedService>();
        foreach (var hs in hostedServices)
        {
            await hs.StartAsync(CancellationToken.None);
        }

        // Assert — each site produced its own "SiteId: <X> registered" log message.
        Assert.True(logCapture.HasMessage("SiteId: DEFAULT registered"),
            "Expected log entry for DEFAULT site registration.");
        Assert.True(logCapture.HasMessage("SiteId: ALPHA registered"),
            "Expected log entry for ALPHA site registration.");
        Assert.True(logCapture.HasMessage("SiteId: BRAVO registered"),
            "Expected log entry for BRAVO site registration.");

        // Exactly 3 distinct registration messages (no duplicates or missing entries).
        var registeredEntries = logCapture.FindEntries("registered").ToList();
        Assert.Equal(3, registeredEntries.Count);
    }

    // -------------------------------------------------------------------------
    // BTEL-02 — SiteCachingBehavior prefix tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// BTEL-02: SiteCachingBehavior registers a keyed ISiteCacheKeyPrefix per site.
    /// Each site's prefix must equal "site:{siteId}:" and be distinct across sites.
    /// </summary>
    [Fact]
    public void SiteCachingBehavior_RegistersPrefixedCacheKey_PerSite()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = EmptyConfig;

        new SiteCachingBehavior().Apply(services, config, "ALPHA");
        new SiteCachingBehavior().Apply(services, config, "BRAVO");

        using var sp = services.BuildServiceProvider();

        // Act
        var alphaPrefix = sp.GetRequiredKeyedService<ISiteCacheKeyPrefix>("ALPHA");
        var bravoPrefix = sp.GetRequiredKeyedService<ISiteCacheKeyPrefix>("BRAVO");

        // Assert
        Assert.Equal("site:ALPHA:", alphaPrefix.Prefix);
        Assert.Equal("site:BRAVO:", bravoPrefix.Prefix);
        Assert.NotEqual(alphaPrefix.Prefix, bravoPrefix.Prefix);
    }

    /// <summary>
    /// BTEL-02 (additional): The resolved prefix string must embed the siteId
    /// and conform to the "site:{siteId}:" format contract.
    /// </summary>
    [Fact]
    public void SiteCachingBehavior_PrefixContainsSiteId()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = EmptyConfig;

        new SiteCachingBehavior().Apply(services, config, "DEFAULT");

        using var sp = services.BuildServiceProvider();

        // Act
        var prefix = sp.GetRequiredKeyedService<ISiteCacheKeyPrefix>("DEFAULT");

        // Assert — prefix follows "site:{siteId}:" format
        Assert.StartsWith("site:", prefix.Prefix);
        Assert.EndsWith(":", prefix.Prefix);
        Assert.Contains("DEFAULT", prefix.Prefix);
        Assert.Equal("site:DEFAULT:", prefix.Prefix);
    }
}
