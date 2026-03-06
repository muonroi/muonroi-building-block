namespace Muonroi.RuleEngine.Core.Tracing;

public interface IRuleExecutionTracer
{
    bool IsEnabled(string? tenantId);
    ValueTask TraceAsync(RuleTraceEntry entry, CancellationToken ct = default);
}
