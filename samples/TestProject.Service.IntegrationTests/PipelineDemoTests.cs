using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Bravo;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25, D-27: Demonstrates MSitePipeline end-to-end execution with Before/After hooks.
/// Proves that BRAVO order creation triggers hooks without any virtual method inheritance.
/// DEFAULT site has no hooks registered — executes default implementation only.
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class PipelineDemoTests
{
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Builds a DI provider with MSitePipeline registered and BRAVO hooks wired.
    /// MSitePipeline is generic over the service context type, resolved as open-generic scoped.
    /// </summary>
    private static (ServiceProvider provider, LogCapture logCapture) BuildProvider(SiteHolder holder)
    {
        var logCapture = new LogCapture();
        var services = new ServiceCollection();
        // IMLog<T> requires SystemExecutionContextAccessor + AddMuonroiLogging()
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b => b.AddMuonroiLogging().AddProvider(logCapture));

        // ISiteProfileResolver stub reads from holder at resolve time
        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));

        // MSitePipeline infrastructure
        services.AddSitePipeline();

        // BRAVO Before hook: validates order.booking_no
        services.AddSiteStepHook<IOrderService>(
            SiteIds.BRAVO,
            "create-order",
            SiteStepHookPhase.Before,
            _ => new BravoValidateOrderHook());

        // BRAVO After hook: enriches with bravo.enriched flag
        services.AddSiteStepHook<IOrderService>(
            SiteIds.BRAVO,
            "create-order",
            SiteStepHookPhase.After,
            _ => new BravoEnrichOrderHook());

        return (services.BuildServiceProvider(validateScopes: true), logCapture);
    }

    [Fact]
    public async Task BravoSite_CreateOrder_TriggersBeforeHook_ValidatesBookingNo()
    {
        // [TEST:BREAKPOINT] PipelineDemoTests - BeforeHook validation
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, logCapture) = BuildProvider(holder);

        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();

        var facts = new FactBag();
        facts.Set("order.booking_no", "BK-2026-001");

        bool defaultImplRan = false;
        await pipeline.RunStep("create-order", facts, (f, ct) =>
        {
            defaultImplRan = true;
            f.Set("order.created", true);
            return Task.CompletedTask;
        });

        // Before hook must have run — validated booking_no and set bravo.validated
        Assert.True(facts.Get<bool>("bravo.validated"), "Before hook should have set bravo.validated=true");
        // Default impl ran (no Replace hooks)
        Assert.True(defaultImplRan, "Default implementation should have run");
        // After hook must have run — enriched facts
        Assert.True(facts.Get<bool>("bravo.enriched"), "After hook should have set bravo.enriched=true");

        // Verify pipeline logs the step markers
        Assert.True(logCapture.HasMessage("create-order"), "Log should contain step name 'create-order'");
        Assert.True(logCapture.HasMessage("BRAVO"), "Log should contain site 'BRAVO'");
    }

    [Fact]
    public async Task BravoSite_CreateOrder_BeforeHookThrows_WhenBookingNoMissing()
    {
        // [TEST:BREAKPOINT] PipelineDemoTests - BeforeHook failure
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, _) = BuildProvider(holder);

        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();

        var facts = new FactBag();
        // No order.booking_no set — should trigger MInternalException

        await Assert.ThrowsAsync<MInternalException>(async () =>
            await pipeline.RunStep("create-order", facts, (f, ct) => Task.CompletedTask));
    }

    [Fact]
    public async Task DefaultSite_CreateOrder_NoHooks_ExecutesDefaultImplOnly()
    {
        // [TEST:BREAKPOINT] PipelineDemoTests - DefaultSite no hooks
        var holder = new SiteHolder { SiteId = SiteIds.DEFAULT };
        var (provider, logCapture) = BuildProvider(holder);

        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();

        var facts = new FactBag();
        bool defaultImplRan = false;

        await pipeline.RunStep("create-order", facts, (f, ct) =>
        {
            defaultImplRan = true;
            f.Set("order.created", true);
            return Task.CompletedTask;
        });

        // No hooks for DEFAULT — only default impl runs
        Assert.True(defaultImplRan, "Default implementation should have run for DEFAULT site");
        // BRAVO-specific facts should NOT be set — no hooks ran
        Assert.False(facts.Get<bool>("bravo.validated"), "bravo.validated should not be set for DEFAULT");
        Assert.False(facts.Get<bool>("bravo.enriched"), "bravo.enriched should not be set for DEFAULT");
        Assert.True(logCapture.HasMessage("DEFAULT"), "Log should contain site 'DEFAULT'");
    }

    [Fact]
    public async Task Pipeline_WorksWithoutVirtualInheritance_D27()
    {
        // [TEST:BREAKPOINT] PipelineDemoTests - NoVirtualInheritance
        // D-27: Prove pipeline works end-to-end without virtual method overrides
        // BravoValidateOrderHook implements ISiteStepHook directly — zero inheritance from service classes

        var hookType = typeof(BravoValidateOrderHook);
        var serviceBase = typeof(object); // ISiteStepHook is the only base — not the service hierarchy

        // Verify hook does NOT extend any service base class
        Assert.DoesNotContain(
            hookType.GetInterfaces(),
            i => i.Name.Contains("OrderService") || i.Name.Contains("ServiceBase"));

        // Verify hook implements ISiteStepHook directly
        Assert.Contains(
            hookType.GetInterfaces(),
            i => i == typeof(ISiteStepHook));

        // Execute the full pipeline to prove end-to-end works
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, _) = BuildProvider(holder);

        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();

        var facts = new FactBag();
        facts.Set("order.booking_no", "BK-D27-TEST");

        await pipeline.RunStep("create-order", facts, (_, _) => Task.CompletedTask);

        Assert.True(facts.Get<bool>("bravo.validated"), "Pipeline end-to-end confirmed without virtual override (D-27)");
    }
}
