namespace Muonroi.RuleEngine.Core.Tracing;

public interface IRuleTraceStore
{
    ValueTask SaveAsync(RuleTraceEntry entry, TimeSpan ttl, CancellationToken ct = default);
    ValueTask<IReadOnlyList<RuleTraceEntry>> QueryAsync(
        string tenantId,
        string? correlationId,
        DateTimeOffset? from,
        CancellationToken ct = default);
}
