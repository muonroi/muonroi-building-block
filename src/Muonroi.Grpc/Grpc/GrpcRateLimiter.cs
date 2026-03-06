using System.Collections.Concurrent;

namespace Muonroi.Grpc.Grpc;

public sealed class GrpcRateLimiter
{
    private readonly ConcurrentDictionary<string, WindowCounter> _apiKeyCounters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WindowCounter> _tenantCounters = new(StringComparer.Ordinal);

    public bool TryConsume(string? apiKey, string? tenantId, GrpcRateLimitConfig config, out string? exceededScope)
    {
        ArgumentNullException.ThrowIfNull(config);

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
