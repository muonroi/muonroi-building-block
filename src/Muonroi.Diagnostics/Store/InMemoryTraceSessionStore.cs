namespace Muonroi.Diagnostics.Store;

/// <summary>
/// In-memory trace session store for development scenarios.
/// </summary>
public sealed class InMemoryTraceSessionStore : ITraceSessionStore
{
    private readonly ConcurrentDictionary<string, MTraceSessionRecord> _sessions = new();

    /// <inheritdoc/>
    public Task SaveAsync(MTraceSessionRecord session, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        _sessions[GetKey(session.SessionId, session.TenantId)] = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<MTraceSessionRecord?> GetAsync(string sessionId, string? tenantId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(GetKey(sessionId, tenantId), out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MTraceSessionRecord>> QueryByTenantAsync(string tenantId, DateTime from, DateTime to, int maxResults = 100, CancellationToken ct = default)
    {
        var results = _sessions.Values
            .Where(s => s.TenantId == tenantId && s.StartedAt >= from && s.StartedAt <= to)
            .OrderByDescending(s => s.StartedAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<MTraceSessionRecord>>(results);
    }

    private static string GetKey(string sessionId, string? tenantId) => $"{tenantId ?? "global"}:{sessionId}";
}
