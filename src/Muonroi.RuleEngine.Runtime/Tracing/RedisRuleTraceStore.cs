using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.Runtime.Tracing;

public sealed class RedisRuleTraceStore(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RuleTracingOptions> options,
    IMJsonSerializeService jsonSerializeService) : IRuleTraceStore
{
    private readonly IConnectionMultiplexer _connectionMultiplexer =
        connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
    private readonly RuleTracingOptions _options = options?.Value ?? new RuleTracingOptions();

    public async ValueTask SaveAsync(RuleTraceEntry entry, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
        {
            ttl = _options.DefaultTtl;
        }

        string key = BuildTraceKey(entry.TenantId, entry.CorrelationId, entry.TraceId);
        string payload = jsonSerializeService.Serialize(entry);
        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        await db.StringSetAsync(key, payload, ttl);
    }

    public async ValueTask<IReadOnlyList<RuleTraceEntry>> QueryAsync(
        string tenantId,
        string? correlationId,
        DateTimeOffset? from,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ct.ThrowIfCancellationRequested();

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        System.Net.EndPoint[] endpoints = _connectionMultiplexer.GetEndPoints();
        if (endpoints.Length == 0)
        {
            return [];
        }

        IServer server = _connectionMultiplexer.GetServer(endpoints[0]);
        string pattern = BuildTracePattern(tenantId, correlationId);

        List<RedisKey> keys = [];
        foreach (RedisKey key in server.Keys(
                     database: _options.Database,
                     pattern: pattern,
                     pageSize: _options.ScanPageSize))
        {
            keys.Add(key);
            if (keys.Count >= _options.MaxQueryResults)
            {
                break;
            }
        }

        if (keys.Count == 0)
        {
            return [];
        }

        RedisValue[] values = await db.StringGetAsync([.. keys]);
        List<RuleTraceEntry> entries = [];
        foreach (RedisValue value in values)
        {
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            RuleTraceEntry? entry = null;
            try
            {
                entry = jsonSerializeService.Deserialize<RuleTraceEntry>(value!);
            }
            catch
            {
                // Corrupt trace payloads are ignored intentionally.
            }

            if (entry is null)
            {
                continue;
            }

            if (from.HasValue && entry.ExecutedAt < from.Value)
            {
                continue;
            }

            entries.Add(entry);
        }

        return [.. entries
            .OrderByDescending(x => x.ExecutedAt)
            .ThenByDescending(x => x.TraceId, StringComparer.Ordinal)];
    }

    private string BuildTraceKey(string tenantId, string correlationId, string traceId)
    {
        string normalizedTenant = string.IsNullOrWhiteSpace(tenantId) ? "unknown" : tenantId.Trim();
        string normalizedCorrelation = string.IsNullOrWhiteSpace(correlationId) ? "none" : correlationId.Trim();
        string normalizedTrace = string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId.Trim();
        return $"{_options.TraceKeyPrefix}:{normalizedTenant}:{normalizedCorrelation}:{normalizedTrace}";
    }

    private string BuildTracePattern(string tenantId, string? correlationId)
    {
        string normalizedTenant = tenantId.Trim();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return $"{_options.TraceKeyPrefix}:{normalizedTenant}:*";
        }

        return $"{_options.TraceKeyPrefix}:{normalizedTenant}:{correlationId.Trim()}:*";
    }
}
