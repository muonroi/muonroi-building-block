namespace Muonroi.AuthZ.HotReload;

/// <summary>
/// Implement this in your application to react when authorization rules change.
/// Typically: invalidate rule cache, force re-evaluation on next request.
/// </summary>
public interface IAuthRuleChangeHandler
{
    /// <summary>
    /// Reacts to a published authorization rule-set change.
    /// </summary>
    Task OnAuthRuleChangedAsync(Guid ruleSetId, CancellationToken ct = default);
}
