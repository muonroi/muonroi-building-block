namespace Muonroi.RuleEngine.Runtime.Web.HotReload;

using Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Consumer-side handler for ruleset change events received from Control Plane.
/// Implement this interface to invalidate the local 3-level cache when a ruleset changes.
/// </summary>
public interface IRuleSetChangeHandlerClient
{
    /// <summary>
    /// Invoked when the Control Plane broadcasts a <see cref="RuleSetChangeEvent"/>.
    /// Implementations should invalidate any cached ruleset data for the affected tenant/workflow.
    /// </summary>
    /// <param name="changeEvent">The ruleset change event containing tenant, workflow, and change type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnRuleSetChangedAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken);
}
