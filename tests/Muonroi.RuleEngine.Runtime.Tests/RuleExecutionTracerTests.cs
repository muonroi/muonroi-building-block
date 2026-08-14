namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleExecutionTracerTests
{
    private readonly IRuleTraceStore _store = Substitute.For<IRuleTraceStore>();
    private readonly IRuleDebuggerModeService _debuggerMode = Substitute.For<IRuleDebuggerModeService>();
    private readonly IOptions<RuleTracingOptions> _options;

    public RuleExecutionTracerTests()
    {
        _options = Options.Create(new RuleTracingOptions());
    }

    [Fact]
    public void IsEnabled_NullTenantId_ReturnsFalse()
    {
        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);

        sut.IsEnabled(null).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhitespaceTenantId_ReturnsFalse()
    {
        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);

        sut.IsEnabled("   ").Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_DebugEnabled_ReturnsTrue()
    {
        _debuggerMode.IsDebugEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);

        sut.IsEnabled("tenant-1").Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_DebugDisabled_ReturnsFalse()
    {
        _debuggerMode.IsDebugEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);

        sut.IsEnabled("tenant-1").Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_CachesResult()
    {
        _debuggerMode.IsDebugEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);

        sut.IsEnabled("tenant-1");
        sut.IsEnabled("tenant-1");

        // Should only call the service once due to caching
        _debuggerMode.Received(1).IsDebugEnabledAsync("tenant-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IsEnabled_WithActiveTraceContext_ReturnsTrue()
    {
        IMTraceContext traceContext = Substitute.For<IMTraceContext>();
        ITraceSession session = Substitute.For<ITraceSession>();
        session.IsActive.Returns(true);
        traceContext.Current.Returns(session);

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options, traceContext);

        // Even with null tenantId, active trace context overrides
        sut.IsEnabled(null).Should().BeTrue();
    }

    [Fact]
    public async Task TraceAsync_WhenDisabled_DoesNotSave()
    {
        _debuggerMode.IsDebugEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);
        RuleTraceEntry entry = new() { TenantId = "t1" };

        await sut.TraceAsync(entry);

        await _store.DidNotReceive().SaveAsync(Arg.Any<RuleTraceEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TraceAsync_WhenEnabled_SavesEntry()
    {
        _debuggerMode.IsDebugEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);
        RuleTraceEntry entry = new() { TenantId = "t1" };

        await sut.TraceAsync(entry);

        await _store.Received(1).SaveAsync(entry, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TraceAsync_NullEntry_Throws()
    {
        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);

        Func<Task> act = async () => await sut.TraceAsync(null!);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task TraceAsync_CancellationRequested_Throws()
    {
        _debuggerMode.IsDebugEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        RuleExecutionTracer sut = new(_store, _debuggerMode, _options);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = async () => await sut.TraceAsync(new RuleTraceEntry { TenantId = "t1" }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        Action act = () => new RuleExecutionTracer(null!, _debuggerMode, _options);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_NullDebuggerMode_Throws()
    {
        Action act = () => new RuleExecutionTracer(_store, null!, _options);

        act.Should().Throw<MArgumentException>();
    }
}
