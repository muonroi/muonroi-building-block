namespace Muonroi.Rules.Rules;

/// <summary>
/// Memory-backed runtime cache with hot invalidation from ruleset change events.
/// </summary>
public sealed class RuleSetRuntimeCache : IRuleSetRuntimeCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly RuleStoreConfigs _configs;
    private readonly IDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleSetRuntimeCache"/> class.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="configs">The rule store configurations.</param>
    /// <param name="notifier">The optional rule set change notifier to subscribe to invalidation events.</param>
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

    /// <summary>
    /// Gets a cached ruleset or creates it using the provided factory if it doesn't exist.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="workflowName">The name of the workflow/ruleset.</param>
    /// <param name="factory">The factory function to create the ruleset if not cached.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the ruleset JSON if found.</returns>
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

    /// <summary>
    /// Invalidates the cache for a specific ruleset.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="workflowName">The name of the workflow/ruleset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
