using Muonroi.RuleEngine.Core.Tracing;

namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// A null object implementation of <see cref="IRuleTraceStore"/> that does nothing.
/// </summary>
public sealed class NullRuleTraceStore : IRuleTraceStore
{
    /// <inheritdoc/>
    public ValueTask SaveAsync(RuleTraceEntry entry, TimeSpan ttl, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RuleTraceEntry>> QueryAsync(string tenantId, string? correlationId, DateTimeOffset? from, CancellationToken ct = default)
    {
        return new ValueTask<IReadOnlyList<RuleTraceEntry>>([]);
    }
}
