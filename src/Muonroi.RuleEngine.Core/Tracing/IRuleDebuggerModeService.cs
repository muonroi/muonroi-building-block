namespace Muonroi.RuleEngine.Core.Tracing;

public interface IRuleDebuggerModeService
{
    ValueTask<bool> IsDebugEnabledAsync(string tenantId, CancellationToken ct = default);
    ValueTask EnableAsync(string tenantId, TimeSpan duration, CancellationToken ct = default);
    ValueTask DisableAsync(string tenantId, CancellationToken ct = default);
}
