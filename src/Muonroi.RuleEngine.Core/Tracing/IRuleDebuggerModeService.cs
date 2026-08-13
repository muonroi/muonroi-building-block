namespace Muonroi.RuleEngine.Core.Tracing;

/// <summary>
/// Service to manage rule debugger mode.
/// </summary>
public interface IRuleDebuggerModeService
{
    /// <summary>
    /// Checks if debug mode is enabled for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if debug mode is enabled; otherwise, false.</returns>
    ValueTask<bool> IsDebugEnabledAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Enables debug mode for the specified tenant for a certain duration.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="duration">The duration for which debug mode should be enabled.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask EnableAsync(string tenantId, TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Disables debug mode for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask DisableAsync(string tenantId, CancellationToken ct = default);
}
