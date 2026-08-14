namespace Muonroi.AspNetCore.Services;

/// <summary>
/// An in-memory implementation of <see cref="IRuleChangeStore"/> for testing and local development.
/// </summary>
/// <param name="dateTimeService">The date time service.</param>
public sealed class InMemoryRuleChangeStore(IMDateTimeService dateTimeService) : IRuleChangeStore
{
    private readonly ConcurrentDictionary<string, RuleChangeState> _stateByKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the current rule order for a specific tenant and endpoint route asynchronously.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointRoute">The endpoint route.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of rule codes in their current order.</returns>
    public Task<IReadOnlyList<string>> GetCurrentAsync(
        string tenantId,
        string endpointRoute,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = BuildKey(tenantId, endpointRoute);
        if (!_stateByKey.TryGetValue(key, out RuleChangeState? state))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        lock (state.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<string>>([.. state.CurrentOrder]);
        }
    }

    /// <summary>
    /// Applies a new rule order change asynchronously.
    /// </summary>
    /// <param name="request">The rule order change request.</param>
    /// <param name="appliedBy">The user or system that applied the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="RuleChangeRecord"/> representing the applied change.</returns>
    public Task<RuleChangeRecord> ApplyAsync(
        RuleOrderChangeRequest request,
        string appliedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string tenantId = NormalizeTenantId(request.TenantId);
        string route = NormalizeRoute(request.EndpointRoute);
        string key = BuildKey(tenantId, route);
        RuleChangeState state = _stateByKey.GetOrAdd(key, _ => new RuleChangeState());

        lock (state.SyncRoot)
        {
            List<string> previousOrder = [.. state.CurrentOrder];
            List<string> newOrder = [.. request.OrderedRuleCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            state.CurrentOrder = newOrder;

            RuleChangeRecord record = new()
            {
                TenantId = tenantId,
                EndpointRoute = route,
                PreviousOrder = previousOrder,
                NewOrder = [.. newOrder],
                AppliedAtUtc = dateTimeService.UtcNow(),
                AppliedBy = string.IsNullOrWhiteSpace(appliedBy) ? "system" : appliedBy
            };

            state.History.Add(record);
            return Task.FromResult(record);
        }
    }

    /// <summary>
    /// Rolls back the last rule order change for a specific tenant and endpoint route asynchronously.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointRoute">The endpoint route.</param>
    /// <param name="appliedBy">The user or system performing the rollback.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="RuleChangeRecord"/> representing the rollback state, or null if no history exists.</returns>
    public Task<RuleChangeRecord?> RollbackAsync(
        string tenantId,
        string endpointRoute,
        string appliedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedTenant = NormalizeTenantId(tenantId);
        string normalizedRoute = NormalizeRoute(endpointRoute);
        string key = BuildKey(normalizedTenant, normalizedRoute);
        if (!_stateByKey.TryGetValue(key, out RuleChangeState? state))
        {
            return Task.FromResult<RuleChangeRecord?>(null);
        }

        lock (state.SyncRoot)
        {
            RuleChangeRecord? lastRecord = state.History.LastOrDefault();
            if (lastRecord is null)
            {
                return Task.FromResult<RuleChangeRecord?>(null);
            }

            List<string> previousOrder = [.. state.CurrentOrder];
            state.CurrentOrder = [.. lastRecord.PreviousOrder];

            RuleChangeRecord rollback = new()
            {
                TenantId = normalizedTenant,
                EndpointRoute = normalizedRoute,
                PreviousOrder = previousOrder,
                NewOrder = [.. state.CurrentOrder],
                AppliedAtUtc = dateTimeService.UtcNow(),
                AppliedBy = string.IsNullOrWhiteSpace(appliedBy) ? "system" : appliedBy
            };

            state.History.Add(rollback);
            return Task.FromResult<RuleChangeRecord?>(rollback);
        }
    }

    /// <summary>
    /// Gets the history of rule order changes for a specific tenant and endpoint route asynchronously.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointRoute">The endpoint route.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of <see cref="RuleChangeRecord"/> objects representing the change history.</returns>
    public Task<IReadOnlyList<RuleChangeRecord>> GetHistoryAsync(
        string tenantId,
        string endpointRoute,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = BuildKey(tenantId, endpointRoute);
        if (!_stateByKey.TryGetValue(key, out RuleChangeState? state))
        {
            return Task.FromResult<IReadOnlyList<RuleChangeRecord>>([]);
        }

        lock (state.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<RuleChangeRecord>>([.. state.History]);
        }
    }

    private static string BuildKey(string? tenantId, string? endpointRoute)
    {
        return $"{NormalizeTenantId(tenantId)}:{NormalizeRoute(endpointRoute)}";
    }

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim();
    }

    private static string NormalizeRoute(string? endpointRoute)
    {
        if (string.IsNullOrWhiteSpace(endpointRoute))
        {
            return "/";
        }

        string route = endpointRoute.Trim();
        if (!route.StartsWith('/'))
        {
            route = "/" + route;
        }

        while (route.Contains("//", StringComparison.Ordinal))
        {
            route = route.Replace("//", "/", StringComparison.Ordinal);
        }

        return route;
    }

    private sealed class RuleChangeState
    {
        public object SyncRoot { get; } = new();
        public List<string> CurrentOrder { get; set; } = [];
        public List<RuleChangeRecord> History { get; } = [];
    }
}
