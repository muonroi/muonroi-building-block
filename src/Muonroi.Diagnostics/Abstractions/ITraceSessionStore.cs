using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.Diagnostics.Abstractions;

/// <summary>
/// Persists and queries trace session records.
/// </summary>
public interface ITraceSessionStore
{
    /// <summary>
    /// Saves a trace session record with an optional TTL.
    /// </summary>
    Task SaveAsync(MTraceSessionRecord session, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a trace session by id and tenant.
    /// </summary>
    Task<MTraceSessionRecord?> GetAsync(string sessionId, string? tenantId, CancellationToken ct = default);
    /// <summary>
    /// Queries trace sessions by tenant and time range.
    /// </summary>
    Task<IReadOnlyList<MTraceSessionRecord>> QueryByTenantAsync(string tenantId, DateTime from, DateTime to, int maxResults = 100, CancellationToken ct = default);
}
