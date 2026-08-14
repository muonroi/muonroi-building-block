namespace Muonroi.Grpc.Grpc;

/// <summary>
/// In-memory rate limiter for gRPC calls.
/// </summary>
public sealed class GrpcRateLimiter
{
    private readonly ConcurrentDictionary<string, WindowCounter> _apiKeyCounters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WindowCounter> _tenantCounters = new(StringComparer.Ordinal);

    /// <summary>
    /// Attempts to consume a request for the provided key scopes.
    /// </summary>
    public bool TryConsume(string? apiKey, string? tenantId, GrpcRateLimitConfig config, out string? exceededScope)
    {
        MGuard.NotNull(config);

        exceededScope = null;
        if (!config.Enabled)
        {
            return true;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(apiKey) &&
            config.RequestsPerMinutePerApiKey > 0 &&
            !TryConsume(_apiKeyCounters, apiKey, now, config.RequestsPerMinutePerApiKey))
        {
            exceededScope = "api-key";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tenantId) &&
            config.RequestsPerMinutePerTenant > 0 &&
            !TryConsume(_tenantCounters, tenantId, now, config.RequestsPerMinutePerTenant))
        {
            exceededScope = "tenant";
            return false;
        }

        return true;
    }

    private static bool TryConsume(
        ConcurrentDictionary<string, WindowCounter> store,
        string key,
        DateTimeOffset now,
        int limit)
    {
        WindowCounter counter = store.GetOrAdd(key, _ => new WindowCounter(now));
        return counter.TryIncrement(now, limit);
    }

    private sealed class WindowCounter(DateTimeOffset now)
    {
        private DateTimeOffset _windowStart = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset);
        private int _count;
        private readonly object _gate = new();

        public bool TryIncrement(DateTimeOffset now, int limit)
        {
            lock (_gate)
            {
                DateTimeOffset currentWindow = new(
                    now.Year,
                    now.Month,
                    now.Day,
                    now.Hour,
                    now.Minute,
                    0,
                    now.Offset);

                if (currentWindow > _windowStart)
                {
                    _windowStart = currentWindow;
                    _count = 0;
                }

                if (_count >= limit)
                {
                    return false;
                }

                _count++;
                return true;
            }
        }
    }
}
