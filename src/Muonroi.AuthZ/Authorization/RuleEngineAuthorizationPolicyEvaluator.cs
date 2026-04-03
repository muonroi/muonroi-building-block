namespace Muonroi.AuthZ.Authorization;

using Muonroi.Core.Abstractions.Guards;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Logging.Abstractions;
using Muonroi.Caching.Abstractions.Distributed;

/// <summary>
/// Bridges ASP.NET Core authorization with the Muonroi Rule Engine.
/// Delegates permission evaluation to registered IRule&lt;AuthorizationRuleContext&gt; rules.
/// Results are cached via <see cref="IMCacheService"/> to improve performance.
/// </summary>
internal sealed class RuleEngineAuthorizationPolicyEvaluator(
    IMRuleOrchestrator<AuthorizationRuleContext> orchestrator,
    IMCacheService cacheService,
    IMLog<RuleEngineAuthorizationPolicyEvaluator>? logger = null)
    : IAuthorizationPolicyEvaluator
{
    private const string CacheNamespace = "authz:decision";

    public async Task<AuthorizationResult> EvaluateAsync(
        AuthorizationRuleContext context,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(context);

        string cacheKey = $"{context.UserId}:{context.Resource}:{context.Action}";
        
        try
        {
            // Try to get decision from cache first
            var cachedResult = await cacheService.GetAsync<AuthorizationResult>(cacheKey, cancellationToken);
            if (cachedResult is not null)
            {
                logger?.Debug("[AuthZ] Cache HIT — User:{UserId} Resource:{Resource} Action:{Action} Decision:{IsAuthorized}",
                    context.UserId, context.Resource, context.Action, cachedResult.IsAuthorized);
                return cachedResult;
            }

            OrchestratorResult result = await orchestrator.ExecuteAsync(context, cancellationToken);

            AuthorizationResult finalDecision;
            if (result.IsSuccess)
            {
                logger?.Debug("[AuthZ] Access GRANTED — User:{UserId} Resource:{Resource} Action:{Action}",
                    context.UserId, context.Resource, context.Action);
                finalDecision = AuthorizationResult.Allow();
            }
            else
            {
                string reason = result.Errors.Count > 0 
                    ? result.Errors[0] 
                    : "Authorization rule denied access";
                    
                logger?.Info("[AuthZ] Access DENIED — User:{UserId} Resource:{Resource} Action:{Action} Reason:{Reason}",
                    context.UserId, context.Resource, context.Action, reason);
                finalDecision = AuthorizationResult.Deny(reason);
            }

            // Cache the decision for 1 minute (short-lived to allow dynamic changes but reduce storming)
            await cacheService.SetAsync(cacheKey, finalDecision, new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                KeyNamespace = CacheNamespace,
                TenantScoped = true
            }, cancellationToken);

            return finalDecision;
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[AuthZ] Evaluation error — User:{UserId} Resource:{Resource}",
                context.UserId, context.Resource);
            return AuthorizationResult.Deny("Authorization evaluation failed");
        }
    }
}
