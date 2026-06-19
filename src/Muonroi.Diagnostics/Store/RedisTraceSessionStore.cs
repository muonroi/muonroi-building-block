using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using StackExchange.Redis;

namespace Muonroi.Diagnostics.Store;

/// <summary>
/// Redis-backed trace session store.
/// </summary>
public sealed class RedisTraceSessionStore(IConnectionMultiplexer redis, IMJsonSerializeService json) : ITraceSessionStore
{
    private readonly IDatabase _db = redis.GetDatabase();

    /// <inheritdoc/>
    public async Task SaveAsync(MTraceSessionRecord session, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var tenantId = session.TenantId ?? "global";
        var sessionKey = $"trace:session:{tenantId}:{session.SessionId}";
        var indexKey = $"trace:sessions:{tenantId}:{session.StartedAt:yyyyMMdd}";

        var data = json.Serialize(session);
        var expiry = ttl ?? TimeSpan.FromHours(24);

        var batch = _db.CreateBatch();
        _ = batch.StringSetAsync(sessionKey, data, expiry);
        _ = batch.SortedSetAddAsync(indexKey, session.SessionId, session.StartedAt.Ticks);
        _ = batch.KeyExpireAsync(indexKey, expiry);
        
        batch.Execute();
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<MTraceSessionRecord?> GetAsync(string sessionId, string? tenantId, CancellationToken ct = default)
    {
        var key = $"trace:session:{tenantId ?? "global"}:{sessionId}";
        var data = await _db.StringGetAsync(key);
        return data.HasValue ? json.Deserialize<MTraceSessionRecord>(MGuard.NotNull((string?)data)) : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MTraceSessionRecord>> QueryByTenantAsync(string tenantId, DateTime from, DateTime to, int maxResults = 100, CancellationToken ct = default)
    {
        var dateKey = $"trace:sessions:{tenantId}:{from:yyyyMMdd}";
        var members = await _db.SortedSetRangeByScoreAsync(dateKey, from.Ticks, to.Ticks, order: Order.Descending, take: maxResults);

        var tasks = members.Select(m => GetAsync(m.ToString(), tenantId, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).Cast<MTraceSessionRecord>().ToList();
    }
}
