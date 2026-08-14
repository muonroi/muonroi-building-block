namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Memory-backed runtime cache with hot invalidation from ruleset change events.
/// </summary>
public sealed class RuleSetRuntimeCache : IRuleSetRuntimeCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly RuleStoreConfigs _configs;
    private readonly IDisposable? _subscription;

    /// <summary>Creates a runtime cache for ruleset content.</summary>
    /// <param name="cache">Memory cache.</param>
    /// <param name="configs">Ruleset store configuration.</param>
    /// <param name="notifier">Optional change notifier for invalidation.</param>
    public RuleSetRuntimeCache(IMemoryCache cache, IOptions<RuleStoreConfigs> configs, IRuleSetChangeNotifier? notifier = null)
    {
        _cache = cache;
        _configs = configs.Value;

        if (notifier is not null)
        {
            _subscription = notifier.Subscribe(changeEvent =>
                InvalidateAsync(changeEvent.TenantId, changeEvent.WorkflowName));
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
