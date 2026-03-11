namespace Muonroi.AuthZ.Authorization;

using Muonroi.RuleEngine.Abstractions;
using Muonroi.Logging.Abstractions;

/// <summary>
/// Bridges ASP.NET Core authorization with the Muonroi Rule Engine.
/// Delegates permission evaluation to registered IRule&lt;AuthorizationRuleContext&gt; rules.
/// </summary>
internal sealed class RuleEngineAuthorizationPolicyEvaluator(
    IMRuleOrchestrator<AuthorizationRuleContext> orchestrator,
    IMLog<RuleEngineAuthorizationPolicyEvaluator>? logger = null)
    : IAuthorizationPolicyEvaluator
{
    public async Task<AuthorizationResult> EvaluateAsync(
        AuthorizationRuleContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            OrchestratorResult result = await orchestrator.ExecuteAsync(context, cancellationToken);

            if (result.IsSuccess)
            {
                logger?.Debug("[AuthZ] Access GRANTED — User:{UserId} Resource:{Resource} Action:{Action}",
                    context.UserId, context.Resource, context.Action);
                return AuthorizationResult.Allow();
            }

            // Extract the first failure reason if available
            string reason = result.Errors.Count > 0 
                ? result.Errors[0] 
                : "Authorization rule denied access";
                
            logger?.Info("[AuthZ] Access DENIED — User:{UserId} Resource:{Resource} Action:{Action} Reason:{Reason}",
                context.UserId, context.Resource, context.Action, reason);
            return AuthorizationResult.Deny(reason);
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[AuthZ] Evaluation error — User:{UserId} Resource:{Resource}",
                context.UserId, context.Resource);
            return AuthorizationResult.Deny("Authorization evaluation failed");
        }
    }
}
