using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Diagnostics.Abstractions;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Behaviours;

namespace Muonroi.Mediator.Tests.Behaviours;

public sealed class MDiagnosticsBehaviorTests
{
    [Fact]
    public async Task Handle_WhenTracingOff_ShouldOnlyInvokeNext()
    {
        TestTraceContext traceContext = new();
        TestTraceSessionStore store = new();
        MDiagnosticsBehavior<TestRequest, string> sut = new(
            new TestLog<MDiagnosticsBehavior<TestRequest, string>>(),
            traceContext,
            store,
            CreateAccessor(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        string result = await sut.Handle(new TestRequest("hello"), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
        traceContext.BeginCalls.Should().Be(0);
        store.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTracingEnabled_ShouldCaptureAndPersistSession()
    {
        TestTraceContext traceContext = new();
        TestTraceSessionStore store = new();
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Muonroi-Trace"] = "deep";

        MDiagnosticsBehavior<TestRequest, string> sut = new(
            new TestLog<MDiagnosticsBehavior<TestRequest, string>>(),
            traceContext,
            store,
            CreateAccessor(),
            new HttpContextAccessor { HttpContext = httpContext });

        string result = await sut.Handle(new TestRequest("hello"), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
        traceContext.BeginCalls.Should().Be(1);
        traceContext.Session.RecordedMessages.Should().Contain("Request body captured");
        traceContext.Session.RecordedMessages.Should().Contain("Response body captured");
        store.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenNextThrows_ShouldMarkSessionFailed_AndPersist()
    {
        TestTraceContext traceContext = new();
        TestTraceSessionStore store = new();
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Muonroi-Trace"] = "true";

        MDiagnosticsBehavior<TestRequest, string> sut = new(
            new TestLog<MDiagnosticsBehavior<TestRequest, string>>(),
            traceContext,
            store,
            CreateAccessor(),
            new HttpContextAccessor { HttpContext = httpContext });

        Func<Task> action = async () => await sut.Handle(
            new TestRequest("hello"),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
        traceContext.Session.FailedReason.Should().Be("boom");
        store.Saved.Should().ContainSingle();
    }

    private static ISystemExecutionContextAccessor CreateAccessor()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext("tenant-a", "user-a", "alice", "corr-1", null, null, true, [], "tests"));
        return accessor;
    }

    private sealed record TestRequest(string Value) : IRequest<string>;

    private sealed class TestTraceContext : IMTraceContext
    {
        public int BeginCalls { get; private set; }
        public TestTraceSession Session { get; } = new();
        public ITraceSession? Current => Session;

        public IDisposable Begin(string sessionId, string? tenantId, string? userId, bool lineTraceEnabled = false)
        {
            BeginCalls++;
            Session.SessionIdValue = sessionId;
            Session.TenantIdValue = tenantId;
            Session.UserIdValue = userId;
            Session.LineTraceEnabledValue = lineTraceEnabled;
            return new DisposeAction(() => { });
        }
    }

    private sealed class TestTraceSessionStore : ITraceSessionStore
    {
        public List<MTraceSessionRecord> Saved { get; } = [];
        public Task SaveAsync(MTraceSessionRecord session, TimeSpan? ttl = null, CancellationToken ct = default)
        {
            Saved.Add(session);
            return Task.CompletedTask;
        }

        public Task<MTraceSessionRecord?> GetAsync(string sessionId, string? tenantId, CancellationToken ct = default)
            => Task.FromResult<MTraceSessionRecord?>(null);

        public Task<IReadOnlyList<MTraceSessionRecord>> QueryByTenantAsync(string tenantId, DateTime from, DateTime to, int maxResults = 100, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MTraceSessionRecord>>([]);
    }

    private sealed class TestTraceSession : ITraceSession
    {
        public string SessionId => SessionIdValue;
        public string? TenantId => TenantIdValue;
        public string? UserId => UserIdValue;
        public bool IsActive => true;
        public bool IsLineTraceEnabled => LineTraceEnabledValue;
        public string SessionIdValue { get; set; } = "corr-1";
        public string? TenantIdValue { get; set; }
        public string? UserIdValue { get; set; }
        public bool LineTraceEnabledValue { get; set; }
        public string? FailedReason { get; private set; }
        public List<string> RecordedMessages { get; } = [];

        public IDisposable BeginNode(string name, MTraceNodeType type) => new DisposeAction(() => { });
        public void Record(string message, object? payload = null) => RecordedMessages.Add(message);
        public void RecordFactSnapshot(string phase, IReadOnlyDictionary<string, object?> facts) { }
        public void RecordLineTrace(int line, string variable, object? value, string? sourceMember = null) { }
        public void RecordBranchTrace(int line, string condition, bool taken) { }
        public void MarkFailed(string reason, Exception? ex = null) => FailedReason = reason;
        public MTraceSessionRecord Export() => new() { SessionId = SessionIdValue, TenantId = TenantIdValue, UserId = UserIdValue };
    }

    private sealed class TestLog<T> : IMLog<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        public IMLogContextScope BeginProperty(string key, object? value) => new DisposeAction(() => { });
        public void Info(string messageTemplate, params object?[] args) { }
        public void Warn(string messageTemplate, params object?[] args) { }
        public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
        public void Debug(string messageTemplate, params object?[] args) { }
        public void InfoTrace(string messageTemplate, params object?[] args) { }
    }

    private sealed class DisposeAction(Action onDispose) : IDisposable, IMLogContextScope
    {
        public void Dispose() => onDispose();
    }
}
