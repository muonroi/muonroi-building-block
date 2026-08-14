namespace Muonroi.Governance.Enterprise.Policy;

/// <summary>
/// Enforces license policy rules at runtime, including rate limiting and quotas.
/// </summary>
public sealed class PolicyEnforcer(LicensePolicy? policy, IMDateTimeService dateTimeService, IMLog<PolicyEnforcer>? logger = null)
{
    // In-memory rate limiting tracking
    private readonly ConcurrentDictionary<string, Counter> _apiCounters = new();
    private readonly ConcurrentDictionary<string, Counter> _dbCounters = new();

    /// <summary>
    /// Gets the Current Policy.
    /// </summary>
    public LicensePolicy? CurrentPolicy => policy;

    /// <summary>
    /// Gets the Is Enforcement Enabled.
    /// </summary>
    public bool IsEnforcementEnabled => policy != null;

    /// <summary>
    /// Checks if an API request is allowed under the current rate limit.
    /// </summary>
    public bool CheckApiRateLimit(string correlationId = "default")
    {
        if (policy == null || policy.Enforcement.MaxApiRequestsPerMinute <= 0)
        {
            return true;
        }

        int limit = policy.Enforcement.MaxApiRequestsPerMinute;
        Counter counter = GetCounter(_apiCounters, dateTimeService.UtcNow().Minute.ToString());

        if (counter.Increment() > limit)
        {
            logger?.Warn("[Policy] API rate limit exceeded: {Limit} req/min", limit);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a database operation is allowed under the current rate limit.
    /// </summary>
    public bool CheckDbRateLimit()
    {
        if (policy == null || policy.Enforcement.MaxDbOperationsPerMinute <= 0)
        {
            return true;
        }

        int limit = policy.Enforcement.MaxDbOperationsPerMinute;
        Counter counter = GetCounter(_dbCounters, dateTimeService.UtcNow().Minute.ToString());

        if (counter.Increment() > limit)
        {
            logger?.Warn("[Policy] DB rate limit exceeded: {Limit} ops/min", limit);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a specific feature quota is available.
    /// </summary>
    public bool CheckFeatureQuota(string featureName, long currentUsage)
    {
        if (policy == null || !policy.FeatureQuotas.TryGetValue(featureName, out FeatureQuota? quota))
        {
            return true;
        }

        if (quota.MaxUsagePerDay > 0 && currentUsage >= quota.MaxUsagePerDay)
        {
            logger?.Warn("[Policy] Feature quota exceeded for '{Feature}': {Limit} units/day", featureName, quota.MaxUsagePerDay);
            return false;
        }

        return true;
    }

    private static Counter GetCounter(ConcurrentDictionary<string, Counter> counters, string key)
    {
        // Clear old counters periodically (simplified here to just the current minute)
        if (counters.Count > 5)
        {
            counters.Clear();
        }

        return counters.GetOrAdd(key, _ => new Counter());
    }

    private sealed class Counter
    {
        private int _count;
        /// <summary>
        /// Executes the Increment operation.
        /// </summary>
        public int Increment()
        {
            return Interlocked.Increment(ref _count);
        }

        /// <summary>
        /// Gets the Value.
        /// </summary>
        public int Value => _count;
    }
}
