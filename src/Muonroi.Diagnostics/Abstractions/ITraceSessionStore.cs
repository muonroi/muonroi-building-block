using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.Diagnostics.Abstractions;

public interface ITraceSessionStore
{
    Task SaveAsync(MTraceSessionRecord session, TimeSpan? ttl = null, CancellationToken ct = default);

    Task<MTraceSessionRecord?> GetAsync(string sessionId, string? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<MTraceSessionRecord>> QueryByTenantAsync(string tenantId, DateTime from, DateTime to, int maxResults = 100, CancellationToken ct = default);
}
