namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for HARD-03 strict-mode behavior in <see cref="TenantRlsDapper{TConn}"/>:
/// throw on missing context, bypass suppression (D-07), strict-off pass-through (criterion #3).
/// All tests are unit tests — no live database required.
/// </summary>
public sealed class StrictModeTests
{
    private static readonly IServiceProvider MinimalSp = BuildMinimalSp();

    private static IServiceProvider BuildMinimalSp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:test"] = "Host=localhost;Database=testdb;Username=test;Password=test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IConnectionStringProvider, TestConnectionStringProvider>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    // -------------------------------------------------------------------------
    // Strict-on: throw on null tenant id
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StrictMode on + null tenant id + no bypass → throws MissingTenantContextException; setter NOT called")]
    public void StrictOn_NullTenantId_NoBypass_ThrowsMissingTenantContextException()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(null), strictMode: true);

        Action act = () => sut.CallEnsureTenantContext();

        act.Should().Throw<MissingTenantContextException>(
            because: "strict-mode must throw when tenant id is null and no bypass is active");
        spy.ApplyCallCount.Should().Be(0,
            because: "the setter must NOT be reached when strict-mode throws");
    }

    // -------------------------------------------------------------------------
    // Strict-on: throw on empty tenant id
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StrictMode on + empty tenant id + no bypass → throws MissingTenantContextException")]
    public void StrictOn_EmptyTenantId_NoBypass_ThrowsMissingTenantContextException()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(""), strictMode: true);

        Action act = () => sut.CallEnsureTenantContext();

        act.Should().Throw<MissingTenantContextException>(
            because: "strict-mode must throw when tenant id is empty (IsNullOrWhiteSpace)");
        spy.ApplyCallCount.Should().Be(0,
            because: "the setter must NOT be reached when strict-mode throws on empty id");
    }

    // -------------------------------------------------------------------------
    // Strict-on: throw on whitespace-only tenant id
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StrictMode on + whitespace-only tenant id + no bypass → throws MissingTenantContextException")]
    public void StrictOn_WhitespaceTenantId_NoBypass_ThrowsMissingTenantContextException()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext("   "), strictMode: true);

        Action act = () => sut.CallEnsureTenantContext();

        act.Should().Throw<MissingTenantContextException>(
            because: "strict-mode must throw when tenant id is whitespace-only (IsNullOrWhiteSpace)");
    }

    // -------------------------------------------------------------------------
    // Strict-on + bypass active: bypass suppresses the throw (D-07)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StrictMode on + null tenant id + inside DapperRlsBypass.Enter() → no throw; setter called once")]
    public void StrictOn_NullTenantId_InsideBypassScope_DoesNotThrow_SetterCalled()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(null), strictMode: true);

        Action act = () =>
        {
            using (DapperRlsBypass.Enter())
            {
                sut.CallEnsureTenantContext();
            }
        };

        act.Should().NotThrow(
            because: "bypass scope suppresses the strict-mode throw (D-07)");
        spy.ApplyCallCount.Should().Be(1,
            because: "when bypass suppresses the throw, the setter must still be called");
    }

    // -------------------------------------------------------------------------
    // Strict-on + non-empty tenant id: no throw, setter called
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StrictMode on + non-empty tenant id → no throw; setter records the id")]
    public void StrictOn_NonEmptyTenantId_DoesNotThrow_SetterCalled()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext("tenant-A"), strictMode: true);

        Action act = () => sut.CallEnsureTenantContext();

        act.Should().NotThrow(
            because: "strict-mode must not throw when a non-empty tenant id is present");
        spy.ApplyCallCount.Should().Be(1);
        spy.Calls[0].TenantId.Should().Be("tenant-A");
    }

    // -------------------------------------------------------------------------
    // Strict-off (default): null tenant id passes through to setter (criterion #3)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StrictMode off + null tenant id → no throw; setter called once (byte-identical to v1.0, criterion #3)")]
    public void StrictOff_NullTenantId_DoesNotThrow_SetterCalledOnce()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(null), strictMode: false);

        Action act = () => sut.CallEnsureTenantContext();

        act.Should().NotThrow(
            because: "strict-off is byte-identical to v1.0 — null tenant id must pass through without throwing (criterion #3)");
        spy.ApplyCallCount.Should().Be(1,
            because: "the setter must be called with null tenant id when strict-mode is off");
        spy.Calls[0].TenantId.Should().BeNull();
    }

    // =========================================================================
    // Async parity: repeat throw, bypass-suppression, and strict-off via CallEnsureTenantContextAsync
    // =========================================================================

    [Fact(DisplayName = "Async: StrictMode on + null tenant id + no bypass → throws MissingTenantContextException; setter NOT called")]
    public async Task Async_StrictOn_NullTenantId_NoBypass_Throws()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(null), strictMode: true);

        Func<Task> act = () => sut.CallEnsureTenantContextAsync();

        await act.Should().ThrowAsync<MissingTenantContextException>(
            because: "async guard must also throw on null tenant id in strict-mode");
        spy.ApplyAsyncCallCount.Should().Be(0,
            because: "the setter must NOT be reached when the async guard throws");
    }

    [Fact(DisplayName = "Async: StrictMode on + null tenant id + inside DapperRlsBypass.Enter() → no throw; setter called once")]
    public async Task Async_StrictOn_NullTenantId_InsideBypassScope_DoesNotThrow()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(null), strictMode: true);

        Func<Task> act = async () =>
        {
            using (DapperRlsBypass.Enter())
            {
                await sut.CallEnsureTenantContextAsync();
            }
        };

        await act.Should().NotThrowAsync(
            because: "async bypass scope suppresses the strict-mode throw (D-07)");
        spy.ApplyAsyncCallCount.Should().Be(1,
            because: "when bypass suppresses the throw on the async path, the setter must still be called");
    }

    [Fact(DisplayName = "Async: StrictMode off + null tenant id → no throw; setter called once (criterion #3)")]
    public async Task Async_StrictOff_NullTenantId_DoesNotThrow_SetterCalledOnce()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = new TestableTenantRlsDapper(MinimalSp, spy, new SpyITenantContext(null), strictMode: false);

        Func<Task> act = () => sut.CallEnsureTenantContextAsync();

        await act.Should().NotThrowAsync(
            because: "strict-off is byte-identical to v1.0 on the async path (criterion #3)");
        spy.ApplyAsyncCallCount.Should().Be(1);
        spy.Calls[0].TenantId.Should().BeNull();
    }
}
