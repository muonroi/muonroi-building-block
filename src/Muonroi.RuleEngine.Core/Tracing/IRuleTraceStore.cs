namespace Muonroi.RuleEngine.Core.Tracing;

/// <summary>
/// Defines a storage mechanism for rule execution traces.
/// </summary>
public interface IRuleTraceStore
{
    /// <summary>
    /// Saves a rule execution trace entry with a specified time-to-live (TTL).
    /// </summary>
    /// <param name="entry">The trace entry to save.</param>
    /// <param name="ttl">The duration for which the trace entry should be retained.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SaveAsync(RuleTraceEntry entry, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Queries trace entries based on tenant and optional criteria.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="correlationId">An optional correlation identifier to filter by.</param>
    /// <param name="from">An optional start timestamp to filter by.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> containing a read-only list of matching <see cref="RuleTraceEntry"/>.</returns>
    ValueTask<IReadOnlyList<RuleTraceEntry>> QueryAsync(
        string tenantId,
        string? correlationId,
        DateTimeOffset? from,
        CancellationToken ct = default);
}
