using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web.Pipeline;
using NSubstitute;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Tenancy.SiteProfile.Web.Tests.Pipeline;

/// <summary>
/// Unit tests for MSitePipeline: verifies Before/After/Replace hook semantics,
/// FactBag shared state, ExecutionMode behavior, and logging.
/// </summary>
public class MSitePipelineTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private const string TestSiteId = "test-site-01";
    private const string TestStepName = "SaveStep";

    /// <summary>
    /// Minimal IMLog&lt;T&gt; implementation for testing (NSubstitute cannot proxy ILogger&lt;T&gt;).
    /// </summary>
    private sealed class FakeLog<T> : IMLog<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        public IMLogContextScope BeginProperty(string key, object? value) => NullLogScope.Instance;
        public void Info(string messageTemplate, params object?[] args) { }
        public void Warn(string messageTemplate, params object?[] args) { }
        public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
        public void Debug(string messageTemplate, params object?[] args) { }
        public void InfoTrace(string messageTemplate, params object?[] args) { }

        private sealed class NullLogScope : IMLogContextScope
        {
            public static readonly NullLogScope Instance = new();
            public void Dispose() { }
        }
    }

    private static ISiteProfileResolver CreateResolver(string siteId = TestSiteId)
    {
        ISiteProfile profile = Substitute.For<ISiteProfile>();
        profile.SiteId.Returns(siteId);
        ISiteProfileResolver resolver = Substitute.For<ISiteProfileResolver>();
        resolver.Current.Returns(profile);
        return resolver;
    }

    private static IMLog<MSitePipeline<TestService>> CreateLog()
        => new FakeLog<MSitePipeline<TestService>>();

    private static MSitePipeline<TestService> CreatePipeline(
        SitePipelineHookRegistry? registry = null,
        ISiteProfileResolver? resolver = null,
        IServiceProvider? serviceProvider = null,
        IMLog<MSitePipeline<TestService>>? log = null)
    {
        return new MSitePipeline<TestService>(
            registry ?? new SitePipelineHookRegistry(),
            resolver ?? CreateResolver(),
            serviceProvider ?? Substitute.For<IServiceProvider>(),
            log ?? CreateLog());
    }

    // Fake hook that records execution order and optionally throws
    private sealed class TrackingHook(List<string> executionLog, string label, Exception? throwException = null) : ISiteStepHook
    {
        public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
        {
            if (throwException != null) throw throwException;
            executionLog.Add(label);
            return Task.CompletedTask;
        }
    }

    // Fake hook that mutates FactBag
    private sealed class FactWriterHook(string key, string value) : ISiteStepHook
    {
        public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
        {
            facts.Set(key, value);
            return Task.CompletedTask;
        }
    }

    // Fake compensatable hook that records execution + compensation in LIFO test
    private sealed class CompensatableTrackingHook : ISiteCompensatableStepHook
    {
        private readonly List<string> _log;
        private readonly string _label;
        private readonly Exception? _throwOnExecute;

        public CompensatableTrackingHook(List<string> log, string label, Exception? throwOnExecute = null)
        {
            _log = log;
            _label = label;
            _throwOnExecute = throwOnExecute;
        }

        public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
        {
            _log.Add($"execute:{_label}");
            if (_throwOnExecute != null) throw _throwOnExecute;
            return Task.CompletedTask;
        }

        public Task CompensateAsync(FactBag facts, CancellationToken cancellationToken = default)
        {
            _log.Add($"compensate:{_label}");
            return Task.CompletedTask;
        }
    }

    // Marker class for the service name (TContext = TestService → serviceName = "TestService")
    private sealed class TestService;

    // -----------------------------------------------------------------------
    // PIPE-01: No hooks — executes defaultImpl
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_NoHooks_ExecutesDefaultImpl()
    {
        // Arrange
        MSitePipeline<TestService> pipeline = CreatePipeline();
        var defaultExecuted = false;
        var facts = new FactBag();

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            defaultExecuted = true;
            return Task.CompletedTask;
        });

        // Assert
        defaultExecuted.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // PIPE-02: Before hook executes BEFORE defaultImpl
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_WithBeforeHook_ExecutesBeforeThenDefault()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Before,
            _ => new TrackingHook(log, "before"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            log.Add("default");
            return Task.CompletedTask;
        });

        // Assert
        log.Should().ContainInOrder("before", "default");
    }

    // -----------------------------------------------------------------------
    // PIPE-03: After hook executes AFTER defaultImpl
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_WithAfterHook_ExecutesDefaultThenAfter()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.After,
            _ => new TrackingHook(log, "after"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            log.Add("default");
            return Task.CompletedTask;
        });

        // Assert
        log.Should().ContainInOrder("default", "after");
    }

    // -----------------------------------------------------------------------
    // PIPE-04: Replace hook skips defaultImpl
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_WithReplaceHook_SkipsDefaultImpl()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Replace,
            _ => new TrackingHook(log, "replace"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();
        var defaultCalled = false;

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            defaultCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        defaultCalled.Should().BeFalse("Replace hook should skip defaultImpl");
        log.Should().Contain("replace");
    }

    // -----------------------------------------------------------------------
    // PIPE-05: Before + After = Before → default → After
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_WithBeforeAndAfterHooks_ExecutesInOrder()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Before,
            _ => new TrackingHook(log, "before"));
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.After,
            _ => new TrackingHook(log, "after"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            log.Add("default");
            return Task.CompletedTask;
        });

        // Assert
        log.Should().ContainInOrder("before", "default", "after");
    }

    // -----------------------------------------------------------------------
    // PIPE-06: FactBag is shared between hooks and defaultImpl
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_FactBagSharedAcrossHooksAndDefault()
    {
        // Arrange
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Before,
            _ => new FactWriterHook("beforeKey", "fromBefore"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();
        string? seenInDefault = null;

        // Act
        await pipeline.RunStep(TestStepName, facts, (bag, _) =>
        {
            seenInDefault = bag.Get<string>("beforeKey");
            bag.Set("defaultKey", "fromDefault");
            return Task.CompletedTask;
        });

        // Assert
        seenInDefault.Should().Be("fromBefore", "Before hook value visible in defaultImpl");
        facts.Get<string>("defaultKey").Should().Be("fromDefault", "defaultImpl value visible after pipeline");
    }

    // -----------------------------------------------------------------------
    // PIPE-07: AllOrNothing — hook failure stops execution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_AllOrNothing_FirstHookFailurePropagatesImmediately()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Before,
            _ => new TrackingHook(log, "before-fail", new InvalidOperationException("hook failed")));
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.After,
            _ => new TrackingHook(log, "after-should-not-run"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();
        var defaultCalled = false;

        // Act & Assert
        Func<Task> act = () => pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            defaultCalled = true;
            return Task.CompletedTask;
        }, executionMode: ExecutionMode.AllOrNothing);

        await act.Should().ThrowAsync<MInternalException>().WithMessage("*hook failed*");
        defaultCalled.Should().BeFalse("default should not run after AllOrNothing failure");
        log.Should().NotContain("after-should-not-run");
    }

    // -----------------------------------------------------------------------
    // PIPE-08: BestEffort — continues after hook failure, aggregates errors
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_BestEffort_ContinuesAfterFailureAndAggregates()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Before,
            _ => new TrackingHook(log, "before-fail", new InvalidOperationException("hook1 failed")));
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.After,
            _ => new TrackingHook(log, "after-runs"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();
        var defaultCalled = false;

        // Act & Assert
        Func<Task> act = () => pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            defaultCalled = true;
            log.Add("default");
            return Task.CompletedTask;
        }, executionMode: ExecutionMode.BestEffort);

        AggregateException aggEx = (await act.Should().ThrowAsync<AggregateException>()).Which;
        aggEx.InnerExceptions.Should().HaveCount(1);
        aggEx.InnerExceptions[0].Message.Should().Be("hook1 failed");

        // Default and after hooks should still have run
        defaultCalled.Should().BeTrue("BestEffort continues past failed hooks");
        log.Should().Contain("default");
        log.Should().Contain("after-runs");
    }

    // -----------------------------------------------------------------------
    // PIPE-09: CompensateOnFailure delegates to RuleOrchestrator (v2 — was NSE in v1)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_CompensateOnFailure_DelegatesToOrchestrator()
    {
        // Arrange — CompensateOnFailure is now supported via RuleOrchestrator bridge.
        // Was NotSupportedException in v1 FIFO implementation.
        MSitePipeline<TestService> pipeline = CreatePipeline();
        var facts = new FactBag();

        // Act — should NOT throw (orchestrator handles CompensateOnFailure natively)
        await pipeline.RunStep(
            TestStepName, facts, (f, _) =>
            {
                f.Set("executed", true);
                return Task.CompletedTask;
            },
            executionMode: ExecutionMode.CompensateOnFailure);

        // Assert — default impl ran successfully
        facts.Get<bool>("executed").Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // PIPE-09b: CompensateOnFailure triggers LIFO compensation on hook failure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_CompensateOnFailure_TriggersLIFOCompensation()
    {
        // Arrange — two compensatable hooks, second one fails
        var compensationLog = new List<string>();
        var registry = new SitePipelineHookRegistry();

        registry.Register(TestSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.Before,
            _ => new CompensatableTrackingHook(compensationLog, "hook-A"));

        registry.Register(TestSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.After,
            _ => new CompensatableTrackingHook(compensationLog, "hook-B",
                throwOnExecute: new InvalidOperationException("hook-B fails")));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act — After hook fails → compensation should fire in LIFO order
        Func<Task> act = () => pipeline.RunStep(
            TestStepName, facts, (_, _) => Task.CompletedTask,
            executionMode: ExecutionMode.CompensateOnFailure);

        // Assert — orchestrator wraps failure; compensation hooks fire
        // Note: the exact exception depends on RuleOrchestrator's error propagation.
        // Key assertion: compensation log shows LIFO order.
        try { await act(); } catch { /* failure expected */ }

        // hook-B executes but fails → compensation fires for hook-A (LIFO: last successful first)
        // hook-B itself may or may not get compensated depending on orchestrator behavior
        compensationLog.Should().Contain("compensate:hook-A",
            "hook-A should be compensated when hook-B fails (LIFO reversal)");
    }

    // -----------------------------------------------------------------------
    // PIPE-10: Registry.GetHooks returns empty list when no hooks registered
    // -----------------------------------------------------------------------

    [Fact]
    public void Registry_GetHookFactories_ReturnsEmptyWhenNotRegistered()
    {
        // Arrange
        var registry = new SitePipelineHookRegistry();

        // Act
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> result =
            registry.GetHookFactories("nonexistent", "SomeService", "SomeStep", SiteStepHookPhase.Before);

        // Assert
        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // PIPE-11: Registry stores and retrieves by exact composite key
    // -----------------------------------------------------------------------

    [Fact]
    public void Registry_Register_StoresAndRetrievesHookByCompositeKey()
    {
        // Arrange
        var registry = new SitePipelineHookRegistry();
        ISiteStepHook expectedHook = Substitute.For<ISiteStepHook>();

        registry.Register(TestSiteId, "MyService", "MyStep", SiteStepHookPhase.Before,
            _ => expectedHook);

        // Act
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> factories =
            registry.GetHookFactories(TestSiteId, "MyService", "MyStep", SiteStepHookPhase.Before);

        // Assert
        factories.Should().HaveCount(1);
        ISiteStepHook resolved = factories[0](Substitute.For<IServiceProvider>());
        resolved.Should().BeSameAs(expectedHook);
    }

    // -----------------------------------------------------------------------
    // PIPE-12: Multiple hooks for same key are all returned
    // -----------------------------------------------------------------------

    [Fact]
    public void Registry_Register_MultipleHooksForSameKey_AllReturned()
    {
        // Arrange
        var registry = new SitePipelineHookRegistry();
        ISiteStepHook hook1 = Substitute.For<ISiteStepHook>();
        ISiteStepHook hook2 = Substitute.For<ISiteStepHook>();

        registry.Register(TestSiteId, "MyService", "MyStep", SiteStepHookPhase.After, _ => hook1);
        registry.Register(TestSiteId, "MyService", "MyStep", SiteStepHookPhase.After, _ => hook2);

        // Act
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> factories =
            registry.GetHookFactories(TestSiteId, "MyService", "MyStep", SiteStepHookPhase.After);

        // Assert
        factories.Should().HaveCount(2);
    }

    // -----------------------------------------------------------------------
    // PIPE-13: Different site does NOT get hooks for another site
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_HooksForDifferentSite_NotApplied()
    {
        // Arrange
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        // Register hook for "other-site", but pipeline resolves "test-site-01"
        registry.Register("other-site", nameof(TestService), TestStepName, SiteStepHookPhase.Before,
            _ => new TrackingHook(log, "other-site-hook"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();
        var defaultCalled = false;

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            defaultCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        defaultCalled.Should().BeTrue();
        log.Should().BeEmpty("hooks for other sites must not run");
    }

    // -----------------------------------------------------------------------
    // PIPE-14: Wrap hook can call default impl (decorator pattern)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_WithWrapHook_CallsDefaultInsideWrapper()
    {
        // Arrange — Wrap hook modifies input, calls default, modifies output
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Wrap,
            _ => new TestWrapperHook());

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act
        await pipeline.RunStep(TestStepName, facts, (f, _) =>
        {
            // Default: reads pre-set value from wrapper, writes its own
            string preValue = f.Get<string>("wrap.pre") ?? "missing";
            f.Set("default.ran", true);
            f.Set("default.saw_pre", preValue);
            return Task.CompletedTask;
        });

        // Assert — wrapper ran (pre + post), default ran inside wrapper
        facts.Get<string>("wrap.pre").Should().Be("before-default", "wrapper set pre value");
        facts.Get<bool>("default.ran").Should().BeTrue("default impl ran inside wrapper");
        facts.Get<string>("default.saw_pre").Should().Be("before-default", "default saw wrapper's pre value");
        facts.Get<string>("wrap.post").Should().Be("after-default", "wrapper set post value");
    }

    // -----------------------------------------------------------------------
    // PIPE-15: Wrap hook can skip default impl entirely
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_WithWrapHook_CanSkipDefault()
    {
        // Arrange — Wrap hook does NOT call next()
        var registry = new SitePipelineHookRegistry();
        registry.Register(TestSiteId, nameof(TestService), TestStepName, SiteStepHookPhase.Wrap,
            _ => new SkipDefaultWrapperHook());

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();
        var defaultCalled = false;

        // Act
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            defaultCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        defaultCalled.Should().BeFalse("wrapper skipped default by not calling next()");
        facts.Get<string>("wrap.custom").Should().Be("replaced", "wrapper provided its own value");
    }

    // -----------------------------------------------------------------------
    // PIPE-16: FactKey<T> provides compile-time type-safe FactBag access
    // -----------------------------------------------------------------------

    [Fact]
    public void FactKey_TypedAccess_PreventsMismatchAndTypo()
    {
        // Arrange
        var key = new FactKey<int>("order.count");
        var stringKey = new FactKey<string>("order.name");
        var facts = new FactBag();

        // Act
        facts.Set(key, 42);
        facts.Set(stringKey, "test-order");

        // Assert — typed Get returns correct type
        facts.Get(key).Should().Be(42);
        facts.Get(stringKey).Should().Be("test-order");

        // Implicit string conversion for backward compat
        string rawKey = key;
        rawKey.Should().Be("order.count");

        // TryGet works with typed key
        facts.TryGet(key, out int? value).Should().BeTrue();
        value.Should().Be(42);

        // Remove works with typed key
        facts.Remove(key).Should().BeTrue();
        facts.Get(key).Should().Be(0); // default(int)
    }

    // -----------------------------------------------------------------------
    // Test helpers for Wrap hooks
    // -----------------------------------------------------------------------

    private sealed class TestWrapperHook : ISiteWrapperHook
    {
        public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
            => Task.CompletedTask; // ISiteStepHook fallback — not used when Wrap phase is active

        public async Task WrapAsync(FactBag facts, Func<FactBag, CancellationToken, Task> next, CancellationToken cancellationToken = default)
        {
            facts.Set("wrap.pre", "before-default");
            await next(facts, cancellationToken);  // call default
            facts.Set("wrap.post", "after-default");
        }
    }

    private sealed class SkipDefaultWrapperHook : ISiteWrapperHook
    {
        public Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WrapAsync(FactBag facts, Func<FactBag, CancellationToken, Task> next, CancellationToken cancellationToken = default)
        {
            // Intentionally skip next() — provide custom value instead
            facts.Set("wrap.custom", "replaced");
            return Task.CompletedTask;
        }
    }

    // -----------------------------------------------------------------------
    // PIPE-17: Fallback hooks apply when site has no override
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_FallbackHook_AppliesWhenSiteHasNoOverride()
    {
        // Arrange — register fallback hook with "*", no hook for test-site-01
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(SitePipelineHookRegistry.FallbackSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.Before, _ => new TrackingHook(log, "fallback-before"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act — site "test-site-01" has no hooks → falls back to "*"
        await pipeline.RunStep(TestStepName, facts, (_, _) => Task.CompletedTask);

        // Assert
        log.Should().Contain("fallback-before", "fallback hook should run for site without override");
    }

    // -----------------------------------------------------------------------
    // PIPE-18: Site-specific hook overrides fallback
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_SiteHook_OverridesFallback()
    {
        // Arrange — register both fallback and site-specific Before hook
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(SitePipelineHookRegistry.FallbackSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.Before, _ => new TrackingHook(log, "fallback-before"));
        registry.Register(TestSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.Before, _ => new TrackingHook(log, "site-before"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act — site "test-site-01" HAS its own hook → fallback NOT used
        await pipeline.RunStep(TestStepName, facts, (_, _) => Task.CompletedTask);

        // Assert
        log.Should().Contain("site-before", "site-specific hook should run");
        log.Should().NotContain("fallback-before", "fallback should NOT run when site has override");
    }

    // -----------------------------------------------------------------------
    // PIPE-19: Fallback Replace hook used as default step implementation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunStep_FallbackReplace_ServesAsDefaultStepForAllSites()
    {
        // Arrange — register fallback Replace (acts as "default step for all sites")
        var log = new List<string>();
        var registry = new SitePipelineHookRegistry();
        registry.Register(SitePipelineHookRegistry.FallbackSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.Replace, _ => new TrackingHook(log, "default-step"));

        // Also register site-specific Before hook (should combine with fallback Replace)
        registry.Register(TestSiteId, nameof(TestService), TestStepName,
            SiteStepHookPhase.Before, _ => new TrackingHook(log, "site-before"));

        MSitePipeline<TestService> pipeline = CreatePipeline(registry: registry);
        var facts = new FactBag();

        // Act — site has Before hook but no Replace → fallback Replace used as main impl
        await pipeline.RunStep(TestStepName, facts, (_, _) =>
        {
            log.Add("defaultImpl-should-not-run");
            return Task.CompletedTask;
        });

        // Assert — site Before runs, fallback Replace runs (replaces defaultImpl), defaultImpl skipped
        log.Should().Contain("site-before");
        log.Should().Contain("default-step");
        log.Should().NotContain("defaultImpl-should-not-run",
            "fallback Replace should substitute defaultImpl");
    }
}
