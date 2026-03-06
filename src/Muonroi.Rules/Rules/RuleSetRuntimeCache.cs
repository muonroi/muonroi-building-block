namespace Muonroi.Rules.Rules;

/// <summary>
/// Memory-backed runtime cache with hot invalidation from ruleset change events.
/// </summary>
public sealed class RuleSetRuntimeCache : IRuleSetRuntimeCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly RuleStoreConfigs _configs;
    private readonly IDisposable? _subscription;

    public RuleSetRuntimeCache(IMemoryCache cache, RuleStoreConfigs configs, IRuleSetChangeNotifier? notifier = null)
    {
        _cache = cache;
        _configs = configs;

        if (notifier is not null)
        {
            _subscription = notifier.Subscribe(changeEvent =>
                InvalidateAsync(changeEvent.TenantId, changeEvent.WorkflowName));
        }
    }

    public async Task<string?> GetOrCreateAsync(
        string tenantId,
        string workflowName,
        Func<Task<string?>> factory,
        CancellationToken cancellationToken = default)
    {
        if (!_configs.EnableRuntimeCache)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await factory();
        }

        string key = BuildCacheKey(tenantId, workflowName);
        if (_cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        cancellationToken.ThrowIfCancellationRequested();
        string? value = await factory();
        if (value is not null)
        {
            _cache.Set(key, value, TimeSpan.FromMinutes(_configs.RuntimeCacheMinutes > 0 ? _configs.RuntimeCacheMinutes : 10));
        }

        return value;
    }

    public Task InvalidateAsync(string tenantId, string workflowName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Remove(BuildCacheKey(tenantId, workflowName));
        return Task.CompletedTask;
    }

    private static string BuildCacheKey(string tenantId, string workflowName)
    {
        return $"ruleset:runtime:{tenantId}:{workflowName}".ToLowerInvariant();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
