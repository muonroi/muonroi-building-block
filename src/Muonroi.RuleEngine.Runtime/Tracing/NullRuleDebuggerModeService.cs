namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// A null object implementation of <see cref="IRuleDebuggerModeService"/> that always returns false.
/// </summary>
public sealed class NullRuleDebuggerModeService : IRuleDebuggerModeService
{
    /// <inheritdoc/>
    public ValueTask<bool> IsDebugEnabledAsync(string tenantId, CancellationToken ct = default)
    {
        return new ValueTask<bool>(false);
    }

    /// <inheritdoc/>
    public ValueTask EnableAsync(string tenantId, TimeSpan ttl, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisableAsync(string tenantId, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetDebugModeAsync(string tenantId, bool enabled, TimeSpan? duration = null, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }
}
