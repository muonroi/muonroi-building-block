using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// FAIL-04: Verifies AsyncLocal isolation for SiteProfileScope under concurrent multi-site requests.
///
/// Tests cover: 3 parallel Tasks each with distinct SiteProfileScope resolve the correct SiteId
/// with zero cross-context bleed, scope cleanup after task completion, and nested scope push/pop.
///
/// AsyncLocal guarantees that each Task inherits a copy of the context at the time it is created.
/// SiteProfileScope overrides the copy for that Task's execution chain only.
/// No cross-bleed is possible by design — tests confirm this deterministically.
/// </summary>
public sealed class FailureScenario_AsyncLocalIsolationTests
{
    /// <summary>
    /// Builds a ServiceProvider with all 3 site profiles and all 3 keyed IOrderService implementations.
    /// All keyed services registered so startup validator passes without SkipSiteProfileStartupValidation.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMultiSiteProfiles(
            configuration,
            siteCodeAccessor: _ => SiteIds.DEFAULT,
            assemblies:
            [
                typeof(DefaultSiteProfile).Assembly,
                typeof(AlphaSiteProfile).Assembly,
                typeof(BravoSiteProfile).Assembly
            ]);

        // All 3 keyed registrations — no missing service so validator passes without SkipValidation
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
        services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);
        services.AddKeyedScoped<IOrderService, BravoOrderService>(SiteIds.BRAVO);

        services.AddSiteResolvedService<IOrderService>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task ConcurrentRequests_EachTask_ResolvesCorrectSiteId_NoCrossBleed()
    {
        // Arrange
        using var provider = BuildProvider();

        var siteIds = new[] { SiteIds.DEFAULT, SiteIds.ALPHA, SiteIds.BRAVO };
        var results = new ConcurrentBag<(string expected, string actual)>();

        // Act — 3 Tasks in parallel, each with its own SiteProfileScope
        var tasks = siteIds.Select(expectedSiteId => Task.Run(async () =>
        {
            // Each Task gets its own isolated AsyncLocal context
            var profile = new FakeSiteProfile(expectedSiteId);
            using var scope = SiteProfileScope.ForSite(profile);

            // Simulate async work (ensures AsyncLocal propagates through await)
            await Task.Yield();

            using var diScope = provider.CreateScope();
            var resolver = diScope.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            var actualSiteId = resolver.Current.SiteId;

            results.Add((expectedSiteId, actualSiteId));
        }));

        await Task.WhenAll(tasks);

        // Assert — all 3 tasks resolved the correct SiteId, no cross-bleed
        Assert.Equal(3, results.Count);
        foreach (var (expected, actual) in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task AfterConcurrentTasks_OuterContext_HasNoActiveScope()
    {
        // Arrange
        using var provider = BuildProvider();

        // Assert — no scope active before tasks start
        var outerProfile = new FakeSiteProfile(SiteIds.DEFAULT);
        // Confirm no scope override exists by resolving with accessor (returns DEFAULT per siteCodeAccessor)
        using var preScopeScope = provider.CreateScope();
        var preResolver = preScopeScope.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
        // The siteCodeAccessor always returns DEFAULT — so resolver.Current.SiteId == DEFAULT is expected
        Assert.Equal(SiteIds.DEFAULT, preResolver.Current.SiteId);

        // Act — run tasks that set scopes internally
        var tasks = new[] { SiteIds.ALPHA, SiteIds.BRAVO }.Select(siteId => Task.Run(async () =>
        {
            using var scope = SiteProfileScope.ForSite(new FakeSiteProfile(siteId));
            await Task.Yield();
            // Scope is disposed when this block exits
        }));

        await Task.WhenAll(tasks);

        // Assert — outer context still resolves to DEFAULT (no scope leak from child tasks)
        using var postScopeScope = provider.CreateScope();
        var postResolver = postScopeScope.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
        Assert.Equal(SiteIds.DEFAULT, postResolver.Current.SiteId);
    }

    [Fact]
    public void NestedScope_RestoresPrevious_OnDispose()
    {
        // Arrange
        using var provider = BuildProvider();

        var defaultProfile = new FakeSiteProfile(SiteIds.DEFAULT);
        var alphaProfile = new FakeSiteProfile(SiteIds.ALPHA);

        // Act — create outer scope (DEFAULT), then inner scope (ALPHA)
        using var outerScope = SiteProfileScope.ForSite(defaultProfile);

        // Verify outer scope is active
        using (var scope1 = provider.CreateScope())
        {
            var resolver1 = scope1.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            Assert.Equal(SiteIds.DEFAULT, resolver1.Current.SiteId);
        }

        // Create inner scope — overrides to ALPHA
        var innerScope = SiteProfileScope.ForSite(alphaProfile);

        // Verify inner scope overrides to ALPHA
        using (var scope2 = provider.CreateScope())
        {
            var resolver2 = scope2.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            Assert.Equal(SiteIds.ALPHA, resolver2.Current.SiteId);
        }

        // Dispose inner scope — should restore back to DEFAULT
        innerScope.Dispose();

        // Verify restored to DEFAULT after inner scope disposed
        using (var scope3 = provider.CreateScope())
        {
            var resolver3 = scope3.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            Assert.Equal(SiteIds.DEFAULT, resolver3.Current.SiteId);
        }

        // Outer scope still active — DEFAULT
        using (var scope4 = provider.CreateScope())
        {
            var resolver4 = scope4.ServiceProvider.GetRequiredService<ISiteProfileResolver>();
            Assert.Equal(SiteIds.DEFAULT, resolver4.Current.SiteId);
        }
    }
}
