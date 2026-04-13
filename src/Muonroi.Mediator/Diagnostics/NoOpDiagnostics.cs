using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.Mediator.Diagnostics;

/// <summary>
/// No-op trace context — used as fallback when Muonroi.Diagnostics is not registered.
/// Tracing is effectively disabled; MDiagnosticsBehavior falls through to ActivationLevel.None path.
/// </summary>
internal sealed class NoOpTraceContext : IMTraceContext
{
    public ITraceSession? Current => null;

    public IDisposable Begin(string sessionId, string? tenantId, string? userId, bool lineTraceEnabled = false)
        => NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>
/// No-op session store — used as fallback when Muonroi.Diagnostics is not registered.
/// Save/Get/Query operations are no-ops.
/// </summary>
internal sealed class NoOpTraceSessionStore : ITraceSessionStore
{
    public Task SaveAsync(MTraceSessionRecord session, TimeSpan? ttl = null, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<MTraceSessionRecord?> GetAsync(string sessionId, string? tenantId, CancellationToken ct = default)
        => Task.FromResult<MTraceSessionRecord?>(null);

    public Task<IReadOnlyList<MTraceSessionRecord>> QueryByTenantAsync(string tenantId, DateTime from, DateTime to, int maxResults = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MTraceSessionRecord>>(Array.Empty<MTraceSessionRecord>());
}
