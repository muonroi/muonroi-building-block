namespace Muonroi.AuthZ.Authorization;

/// <summary>
/// Evaluates an authorization request by running it through the Muonroi Rule Engine.
/// Rules are registered as IRule&lt;AuthorizationRuleContext&gt; and can be hot-reloaded
/// from the Control Plane without application restart.
/// </summary>
public interface IAuthorizationPolicyEvaluator
{
    /// <summary>
    /// Evaluates all registered authorization rules against the provided context.
    /// Returns <see cref="AuthorizationResult.Allow()"/> only if all rules pass.
    /// </summary>
    Task<AuthorizationResult> EvaluateAsync(
        AuthorizationRuleContext context,
        CancellationToken cancellationToken = default);
}
