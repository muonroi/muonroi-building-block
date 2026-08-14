namespace Muonroi.AuthZ.Authorization;

/// <summary>
/// Handles MuonroiAuthorizationRequirement by delegating to IAuthorizationPolicyEvaluator.
/// Extracts UserId, TenantId, Roles from the ClaimsPrincipal automatically.
/// </summary>
internal sealed class MuonroiAuthorizationHandler(
    IAuthorizationPolicyEvaluator evaluator,
    ISystemExecutionContextAccessor contextAccessor)
    : AuthorizationHandler<MuonroiAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MuonroiAuthorizationRequirement requirement)
    {
        ISystemExecutionContext executionCtx = contextAccessor.Get();

        // Build authorization context from ClaimsPrincipal + execution context
        AuthorizationRuleContext ruleContext = new()
        {
            UserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? executionCtx.UserId
                ?? string.Empty,
            TenantId = context.User.FindFirst("tenant_id")?.Value
                ?? executionCtx.TenantId
                ?? string.Empty,
            Resource = requirement.Resource,
            Action = requirement.Action,
            Roles = context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList(),
            Claims = context.User.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => (object?)g.First().Value)
        };

        AuthorizationResult result = await evaluator.EvaluateAsync(ruleContext);

        if (result.IsAuthorized)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, result.DeniedReason ?? "Denied"));
        }
    }
}
