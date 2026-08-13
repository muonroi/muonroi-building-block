using System.Diagnostics;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using Xunit.Abstractions;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Performance benchmarks for MSitePipeline.RunStep resolution latency
/// and keyed DI service resolution throughput across 3, 10, and 20 sites.
///
/// Uses Stopwatch-based measurement — not BenchmarkDotNet (too heavy for integration test suite).
/// Results are logged with [BENCHMARK] prefix for CI parsing.
/// Assertions are sanity guards (not strict perf gates) designed to pass on slow CI machines.
///
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class PipelineBenchmarkTests(ITestOutputHelper output)
{
    // -------------------------------------------------------------------------
    // Benchmark configuration
    // -------------------------------------------------------------------------

    private const int WarmupIterations = 50;
    private const int MeasuredIterations = 500;

    /// <summary>
    /// Measures average execution time in microseconds over a fixed number of iterations.
    /// Warmup iterations are discarded to eliminate JIT cold-start bias.
    /// </summary>
    private static async Task<double> MeasureAverageMicroseconds(
        Func<Task> action,
        int warmupIterations = WarmupIterations,
        int measuredIterations = MeasuredIterations)
    {
        // Warmup — discard results to let JIT compile hot paths
        for (int i = 0; i < warmupIterations; i++)
        {
            await action();
        }

        // Measured iterations
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < measuredIterations; i++)
        {
            await action();
        }
        sw.Stop();

        return sw.Elapsed.TotalMicroseconds / measuredIterations;
    }

    // -------------------------------------------------------------------------
    // DI provider builder
    // -------------------------------------------------------------------------

    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Trivial Before hook for benchmarking — sets a single FactBag key.
    /// Avoids any validation logic so we measure pipeline overhead, not hook business logic.
    /// </summary>
    private sealed class TrivialBeforeHook(string siteId) : ISiteStepHook
    {
        public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
        {
            facts.Set($"{siteId}.bench.visited", true);
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildProvider(
        SiteHolder holder,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b => b.AddMuonroiLogging(useAsyncQueue: false));
        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));
        services.AddSitePipeline();
        configure?.Invoke(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    // -------------------------------------------------------------------------
    // Test 1: RunStep latency — 3 sites
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunStep_ResolutionLatency_3Sites()
    {
        // [TEST:BREAKPOINT] PipelineBenchmarkTests - RunStep latency 3 sites
        // One ServiceProvider with hooks registered for DEFAULT, ALPHA, BRAVO.
        // Each site has one Before hook that sets a trivial FactBag key.

        var holder = new SiteHolder();

        using var provider = BuildProvider(holder, svc =>
        {
            // Register a Before hook for each of the 3 sites
            foreach (string siteId in new[] { SiteIds.DEFAULT, SiteIds.ALPHA, SiteIds.BRAVO })
            {
                string capturedSiteId = siteId; // capture for lambda
                svc.AddSiteStepHook<IOrderService>(
                    capturedSiteId,
                    "benchmark-step",
                    SiteStepHookPhase.Before,
                    _ => new TrivialBeforeHook(capturedSiteId));
            }
        });

        output.WriteLine("[TEST:BREAKPOINT] PipelineBenchmarkTests - RunStep latency 3 sites");

        foreach (string siteId in new[] { SiteIds.DEFAULT, SiteIds.ALPHA, SiteIds.BRAVO })
        {
            holder.SiteId = siteId;

            using var scope = provider.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();

            double avgMicroseconds = await MeasureAverageMicroseconds(async () =>
            {
                using var innerScope = provider.CreateScope();
                var innerPipeline = innerScope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
                var facts = new FactBag();
                await innerPipeline.RunStep(
                    "benchmark-step",
                    facts,
                    (_, _) => Task.CompletedTask);
            });

            output.WriteLine($"[BENCHMARK] RunStep latency (3 sites, {siteId}): {avgMicroseconds:F1} us");

            // Sanity guard: must complete in under 5ms per call even on slow CI
            Assert.True(avgMicroseconds < 5000,
                $"RunStep latency for {siteId} ({avgMicroseconds:F1} us) exceeded 5ms sanity threshold");
        }
    }

    // -------------------------------------------------------------------------
    // Test 2: RunStep latency — 10 sites
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunStep_ResolutionLatency_10Sites()
    {
        // [TEST:BREAKPOINT] PipelineBenchmarkTests - RunStep latency 10 sites
        // Register hooks for 10 sites (site_00..site_09).
        // Measure only 3 representative sites (first, middle, last) to keep test time manageable.

        var siteIds = Enumerable.Range(0, 10).Select(i => $"site_{i:D2}").ToArray();
        var holder = new SiteHolder();

        output.WriteLine("[TEST:BREAKPOINT] PipelineBenchmarkTests - RunStep latency 10 sites");

        using var provider = BuildProvider(holder, svc =>
        {
            foreach (string siteId in siteIds)
            {
                string capturedSiteId = siteId;
                svc.AddSiteStepHook<IOrderService>(
                    capturedSiteId,
                    "benchmark-step",
                    SiteStepHookPhase.Before,
                    _ => new TrivialBeforeHook(capturedSiteId));
            }
        });

        // Measure 3 representative sites: first (site_00), middle (site_04), last (site_09)
        string[] representativeSites = [siteIds[0], siteIds[4], siteIds[9]];

        foreach (string siteId in representativeSites)
        {
            holder.SiteId = siteId;

            double avgMicroseconds = await MeasureAverageMicroseconds(async () =>
            {
                using var innerScope = provider.CreateScope();
                var pipeline = innerScope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
                var facts = new FactBag();
                await pipeline.RunStep(
                    "benchmark-step",
                    facts,
                    (_, _) => Task.CompletedTask);
            });

            output.WriteLine($"[BENCHMARK] RunStep latency (10 sites, {siteId}): {avgMicroseconds:F1} us");

            Assert.True(avgMicroseconds < 5000,
                $"RunStep latency for {siteId} ({avgMicroseconds:F1} us) exceeded 5ms sanity threshold");
        }
    }

    // -------------------------------------------------------------------------
    // Test 3: RunStep latency — 20 sites
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunStep_ResolutionLatency_20Sites()
    {
        // [TEST:BREAKPOINT] PipelineBenchmarkTests - RunStep latency 20 sites
        // Register hooks for 20 sites (site_00..site_19).
        // Measure 3 representative sites (first, middle, last).

        var siteIds = Enumerable.Range(0, 20).Select(i => $"site_{i:D2}").ToArray();
        var holder = new SiteHolder();

        output.WriteLine("[TEST:BREAKPOINT] PipelineBenchmarkTests - RunStep latency 20 sites");

        using var provider = BuildProvider(holder, svc =>
        {
            foreach (string siteId in siteIds)
            {
                string capturedSiteId = siteId;
                svc.AddSiteStepHook<IOrderService>(
                    capturedSiteId,
                    "benchmark-step",
                    SiteStepHookPhase.Before,
                    _ => new TrivialBeforeHook(capturedSiteId));
            }
        });

        // Measure 3 representative sites: first (site_00), middle (site_09), last (site_19)
        string[] representativeSites = [siteIds[0], siteIds[9], siteIds[19]];

        foreach (string siteId in representativeSites)
        {
            holder.SiteId = siteId;

            double avgMicroseconds = await MeasureAverageMicroseconds(async () =>
            {
                using var innerScope = provider.CreateScope();
                var pipeline = innerScope.ServiceProvider.GetRequiredService<MSitePipeline<IOrderService>>();
                var facts = new FactBag();
                await pipeline.RunStep(
                    "benchmark-step",
                    facts,
                    (_, _) => Task.CompletedTask);
            });

            output.WriteLine($"[BENCHMARK] RunStep latency (20 sites, {siteId}): {avgMicroseconds:F1} us");

            // Slightly higher threshold for 20 sites — allow up to 10ms on slow CI
            Assert.True(avgMicroseconds < 10000,
                $"RunStep latency for {siteId} ({avgMicroseconds:F1} us) exceeded 10ms sanity threshold");
        }
    }

    // -------------------------------------------------------------------------
    // Test 4: Keyed DI service resolution throughput
    // -------------------------------------------------------------------------

    /// <summary>
    /// Trivial IOrderService implementation for keyed DI resolution benchmark.
    /// Does no real work — exists only to measure DI resolution overhead.
    /// </summary>
    private sealed class BenchmarkOrderService : IOrderService
    {
        public Task<CreateOrderResult> CreateAsync(string name, string description, CancellationToken ct = default)
            => Task.FromResult(new CreateOrderResult { Success = true });

        public Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default)
            => Task.FromResult(new OrderDto(id, null, null));

        public Task<OrderListResult> GetListAsync(string linerCode, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new OrderListResult());
    }

    [Fact]
    public async Task KeyedDI_ServiceResolution_Throughput()
    {
        // [TEST:BREAKPOINT] PipelineBenchmarkTests - Keyed DI resolution throughput
        // Registers BenchmarkOrderService as a keyed scoped service for 3, 10, and 20 sites.
        // Measures how many GetRequiredKeyedService<IOrderService>(siteId) calls per second
        // can be issued from a single DI scope.

        output.WriteLine("[TEST:BREAKPOINT] PipelineBenchmarkTests - Keyed DI resolution throughput");

        foreach (int siteCount in new[] { 3, 10, 20 })
        {
            var siteIds = Enumerable.Range(0, siteCount)
                .Select(i => $"bench_site_{i:D2}")
                .ToArray();

            var services = new ServiceCollection();

            // Register BenchmarkOrderService as keyed scoped for each site
            foreach (string siteId in siteIds)
            {
                services.AddKeyedScoped<IOrderService, BenchmarkOrderService>(siteId);
            }

            await using var provider = services.BuildServiceProvider(validateScopes: false);

            // Measure resolution throughput using a single scope
            // (keyed resolution is the hot path we want to benchmark)
            using var scope = provider.CreateScope();
            string firstSiteId = siteIds[0];

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = scope.ServiceProvider.GetRequiredKeyedService<IOrderService>(firstSiteId);
            }

            // Measure: count how many resolutions happen in 1 second (up to MeasuredIterations)
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasuredIterations; i++)
            {
                // Cycle through all site IDs to exercise the full registry
                string siteId = siteIds[i % siteCount];
                _ = scope.ServiceProvider.GetRequiredKeyedService<IOrderService>(siteId);
            }
            sw.Stop();

            double elapsedSeconds = sw.Elapsed.TotalSeconds;
            double opsPerSecond = MeasuredIterations / elapsedSeconds;

            output.WriteLine(
                $"[BENCHMARK] Keyed DI resolution throughput ({siteCount} sites): {opsPerSecond:N0} ops/sec " +
                $"({elapsedSeconds * 1000 / MeasuredIterations:F2} ms/op avg)");

            // Sanity guard: must achieve at least 10,000 ops/sec
            // (any modern machine should clear this by 10-100x)
            Assert.True(opsPerSecond > 10_000,
                $"Keyed DI resolution throughput for {siteCount} sites ({opsPerSecond:N0} ops/sec) " +
                $"is below 10,000 ops/sec sanity threshold");
        }
    }
}
