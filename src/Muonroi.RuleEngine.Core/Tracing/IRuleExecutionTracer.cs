namespace Muonroi.RuleEngine.Core.Tracing;

/// <summary>
/// Provides a mechanism to trace rule execution events.
/// </summary>
public interface IRuleExecutionTracer
{
    /// <summary>
    /// Checks if tracing is enabled for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns><c>true</c> if tracing is enabled; otherwise, <c>false</c>.</returns>
    bool IsEnabled(string? tenantId);

    /// <summary>
    /// Records a rule execution trace entry asynchronously.
    /// </summary>
    /// <param name="entry">The trace entry details.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask TraceAsync(RuleTraceEntry entry, CancellationToken ct = default);
}
