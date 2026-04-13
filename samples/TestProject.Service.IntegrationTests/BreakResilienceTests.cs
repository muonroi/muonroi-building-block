using System.Collections.Concurrent;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Bravo;
using TestProject.Service.Sites.Default;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Break/resilience scenarios for MSitePipeline.
///
/// Covers:
/// 1. Partial site failure — one site's hook fails, other sites succeed.
/// 2. Concurrent multi-site pipeline — 3 parallel tasks, correct hook dispatch per site.
/// 3. Hot-reload config isolation — config change affects only target site.
/// 4. Pipeline BestEffort mode — one hook throws, pipeline continues, AggregateException collected.
///
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class BreakResilienceTests
{
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Builds a DI provider with MSitePipeline and optional hook registrations.
    /// </summary>
    private static ServiceProvider BuildProvider(
        SiteHolder holder,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        // IMLog<T> requires SystemExecutionContextAccessor + AddMuonroiLogging()
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b => b.AddMuonroiLogging());

        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));
        services.AddSitePipeline();

        configure?.Invoke(services);

        return services.BuildServiceProvider(validateScopes: true);
    }

    // -------------------------------------------------------------------------
    // 1. Partial site failure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PartialSiteFailure_BravoHookThrows_DefaultSucceeds_NoBleed()
    {
        // [TEST:BREAKPOINT] BreakResilienceTests - PartialSiteFailure
        // BRAVO hook is configured to throw (simulates a broken hook for BRAVO site).
        // DEFAULT has no hooks — must succeed even while BRAVO fails.

        // --- BRAVO run (should fail) ---
        var bravoHolder = new SiteHolder { SiteId = SiteIds.BRAVO };
        using var bravoProvider = BuildProvider(bravoHolder, svc =>
        {
            // BRAVO Before hook always throws (simulates broken validation / failed DB check)
            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.Before,
                _ => new BravoFailingHook());
        });

        using var bravoScope = bravoProvider.CreateScope();
        var bravoPipeline = bravoScope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
        var bravoFacts = new FactBag();

        var bravoException = await Record.ExceptionAsync(async () =>
            await bravoPipeline.RunStep("create-order", bravoFacts, (_, _) => Task.CompletedTask));

        // BRAVO should fail
        Assert.NotNull(bravoException);
        Assert.IsType<MInternalException>(bravoException);
        Assert.Contains("intentional failure", bravoException.Message);

        // --- DEFAULT run (should succeed) ---
        var defaultHolder = new SiteHolder { SiteId = SiteIds.DEFAULT };
        using var defaultProvider = BuildProvider(defaultHolder); // no hooks registered for DEFAULT

        using var defaultScope = defaultProvider.CreateScope();
        var defaultPipeline = defaultScope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
        var defaultFacts = new FactBag();
        bool defaultRan = false;

        var defaultException = await Record.ExceptionAsync(async () =>
            await defaultPipeline.RunStep("create-order", defaultFacts, (_, _) =>
            {
                defaultRan = true;
                return Task.CompletedTask;
            }));

        // DEFAULT should succeed — BRAVO failure does not cascade
        Assert.Null(defaultException);
        Assert.True(defaultRan, "DEFAULT pipeline default impl should have run without BRAVO failure cascading");
    }

    // -------------------------------------------------------------------------
    // 2. Concurrent multi-site pipeline
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentPipeline_ThreeSites_EachGetsCorrectHooks_NoFactBagBleed()
    {
        // [TEST:BREAKPOINT] BreakResilienceTests - ConcurrentPipeline
        // 3 parallel Tasks run MSitePipeline for different sites simultaneously.
        // Each task should resolve ONLY its site's hooks; FactBag state must not bleed.

        var results = new ConcurrentDictionary<string, bool>();

        async Task RunSiteTask(string siteId, bool hasBravoHooks)
        {
            var holder = new SiteHolder { SiteId = siteId };
            using var provider = BuildProvider(holder, svc =>
            {
                if (hasBravoHooks)
                {
                    // BRAVO site gets Before hook that sets bravo.validated
                    svc.AddSiteStepHook<IOrderService>(
                        SiteIds.BRAVO,
                        "create-order",
                        SiteStepHookPhase.Before,
                        _ => new BravoValidateOrderHook());
                }
            });

            using var scope = provider.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
            var facts = new FactBag();

            if (siteId == SiteIds.BRAVO)
                facts.Set("order.booking_no", $"BK-{siteId}-CONCURRENT");

            await pipeline.RunStep("create-order", facts, (f, _) =>
            {
                f.Set($"{siteId}.default_ran", true);
                return Task.CompletedTask;
            });

            // Capture outcomes per site
            bool bravoValidated = facts.Get<bool>("bravo.validated");
            bool defaultRan = facts.Get<bool>($"{siteId}.default_ran");

            results[$"{siteId}.validated"] = bravoValidated;
            results[$"{siteId}.default_ran"] = defaultRan;
        }

        // Run 3 sites in parallel
        await Task.WhenAll(
            RunSiteTask(SiteIds.BRAVO, hasBravoHooks: true),
            RunSiteTask(SiteIds.DEFAULT, hasBravoHooks: false),
            RunSiteTask(SiteIds.ALPHA, hasBravoHooks: false)
        );

        // [TEST:BREAKPOINT] BreakResilienceTests - ConcurrentPipeline assertions
        // BRAVO should have its Before hook run
        Assert.True(results[$"{SiteIds.BRAVO}.validated"], "BRAVO Before hook should have set bravo.validated=true");
        // DEFAULT and ALPHA should NOT have bravo.validated set (no bleed from BRAVO)
        Assert.False(results[$"{SiteIds.DEFAULT}.validated"], "DEFAULT FactBag should not have bravo.validated");
        Assert.False(results[$"{SiteIds.ALPHA}.validated"], "ALPHA FactBag should not have bravo.validated");
        // All three sites ran their default implementation
        Assert.True(results[$"{SiteIds.BRAVO}.default_ran"], "BRAVO default impl should have run");
        Assert.True(results[$"{SiteIds.DEFAULT}.default_ran"], "DEFAULT default impl should have run");
        Assert.True(results[$"{SiteIds.ALPHA}.default_ran"], "ALPHA default impl should have run");
    }

    // -------------------------------------------------------------------------
    // 3. Hot-reload config isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HotReloadConfigIsolation_BravoHookAdded_DefaultPipelineUnaffected()
    {
        // [TEST:BREAKPOINT] BreakResilienceTests - HotReloadConfigIsolation
        // Simulates the effect of a runtime config change:
        // A new hook is added for BRAVO; DEFAULT should remain unaffected.
        //
        // DI-based approach: two separate ServiceProviders are built sequentially.
        // First build: only BRAVO Before hook. Second build adds After hook.
        // DEFAULT pipeline never gains any hooks — isolating config changes per site.

        // Build 1: BRAVO has only Before hook
        var bravoHolder = new SiteHolder { SiteId = SiteIds.BRAVO };
        using var provider1 = BuildProvider(bravoHolder, svc =>
        {
            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.Before,
                _ => new BravoValidateOrderHook());
        });

        using var scope1 = provider1.CreateScope();
        var pipeline1 = scope1.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
        var facts1 = new FactBag();
        facts1.Set("order.booking_no", "BK-HOTRELOAD-1");

        await pipeline1.RunStep("create-order", facts1, (_, _) => Task.CompletedTask);

        // Before hook ran, After hook did NOT run (not registered in build 1)
        Assert.True(facts1.Get<bool>("bravo.validated"), "Before hook should run in build 1");
        Assert.False(facts1.Get<bool>("bravo.enriched"), "After hook should NOT run in build 1");

        // Build 2: BRAVO now has both Before AND After hooks (simulates hot-reload adding new hook)
        using var provider2 = BuildProvider(bravoHolder, svc =>
        {
            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.Before,
                _ => new BravoValidateOrderHook());

            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.After,
                _ => new BravoEnrichOrderHook());
        });

        using var scope2 = provider2.CreateScope();
        var pipeline2 = scope2.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
        var facts2 = new FactBag();
        facts2.Set("order.booking_no", "BK-HOTRELOAD-2");

        await pipeline2.RunStep("create-order", facts2, (_, _) => Task.CompletedTask);

        // Both hooks ran in build 2
        Assert.True(facts2.Get<bool>("bravo.validated"), "Before hook should run in build 2");
        Assert.True(facts2.Get<bool>("bravo.enriched"), "After hook should run in build 2 (hot-reload added it)");

        // [TEST:BREAKPOINT] BreakResilienceTests - Verify DEFAULT pipeline unaffected
        // DEFAULT pipeline built independently — no hooks ever registered
        var defaultHolder = new SiteHolder { SiteId = SiteIds.DEFAULT };
        using var defaultProvider = BuildProvider(defaultHolder);
        using var defaultScope = defaultProvider.CreateScope();
        var defaultPipeline = defaultScope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
        var defaultFacts = new FactBag();
        bool defaultRan = false;

        await defaultPipeline.RunStep("create-order", defaultFacts,
            (_, _) => { defaultRan = true; return Task.CompletedTask; });

        Assert.True(defaultRan, "DEFAULT default impl ran");
        Assert.False(defaultFacts.Get<bool>("bravo.validated"), "DEFAULT should not have bravo.validated — config isolated");
        Assert.False(defaultFacts.Get<bool>("bravo.enriched"), "DEFAULT should not have bravo.enriched — config isolated");
    }

    // -------------------------------------------------------------------------
    // 4. Pipeline hook failure resilience (BestEffort mode)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BestEffortMode_OneHookFails_OtherHooksContinue_AggregateExceptionCollected()
    {
        // [TEST:BREAKPOINT] BreakResilienceTests - BestEffort
        // BestEffort mode: if one hook throws, the remaining hooks still execute.
        // After all hooks complete, a single AggregateException is thrown containing all failures.

        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        using var provider = BuildProvider(holder, svc =>
        {
            // First Before hook: FAILS (BravoFailingHook always throws)
            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.Before,
                _ => new BravoFailingHook());

            // Second Before hook: SUCCEEDS (BravoValidateOrderHook succeeds when booking_no present)
            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.Before,
                _ => new BravoValidateOrderHook());

            // After hook: SUCCEEDS (BravoEnrichOrderHook always succeeds)
            svc.AddSiteStepHook<IOrderService>(
                SiteIds.BRAVO,
                "create-order",
                SiteStepHookPhase.After,
                _ => new BravoEnrichOrderHook());
        });

        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();

        var facts = new FactBag();
        facts.Set("order.booking_no", "BK-BEST-EFFORT");
        bool defaultImplRan = false;

        // BestEffort mode: collect all failures into AggregateException at the end
        var ex = await Record.ExceptionAsync(async () =>
            await pipeline.RunStep(
                "create-order",
                facts,
                (f, _) =>
                {
                    defaultImplRan = true;
                    f.Set("order.created", true);
                    return Task.CompletedTask;
                },
                executionMode: ExecutionMode.BestEffort));

        // [TEST:BREAKPOINT] BreakResilienceTests - AggregateException assertions
        // Must throw AggregateException with exactly 1 inner exception (from BravoFailingHook)
        Assert.NotNull(ex);
        var aggEx = Assert.IsType<AggregateException>(ex);
        Assert.Single(aggEx.InnerExceptions);
        Assert.Contains("intentional failure", aggEx.InnerExceptions[0].Message);

        // Remaining hooks (BravoValidateOrderHook, BravoEnrichOrderHook) ran despite first hook failing
        Assert.True(facts.Get<bool>("bravo.validated"), "BravoValidateOrderHook should have run after first hook failed");
        Assert.True(facts.Get<bool>("bravo.enriched"), "BravoEnrichOrderHook (After hook) should have run");
        // Default implementation also ran
        Assert.True(defaultImplRan, "Default impl should have run in BestEffort mode despite hook failure");
    }
}
