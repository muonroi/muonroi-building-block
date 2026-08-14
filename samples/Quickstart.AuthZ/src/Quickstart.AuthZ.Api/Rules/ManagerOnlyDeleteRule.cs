namespace Quickstart.AuthZ.Api.Rules;

/// <summary>
/// Example authorization rule: only users with the "manager" role may perform
/// the "delete" action. All other actions pass.
///
/// Rules are registered as IRule&lt;AuthorizationRuleContext&gt; and executed by
/// the RuleEngine via RuleEngineAuthorizationPolicyEvaluator. Returning
/// RuleResult.Failure(...) causes the evaluator to produce AuthorizationResult.Deny.
/// </summary>
public sealed class ManagerOnlyDeleteRule : IRule<AuthorizationRuleContext>
{
    public string Code => "authz.manager-only-delete";

    public Task<RuleResult> EvaluateAsync(
        AuthorizationRuleContext ctx, FactBag facts, CancellationToken ct)
    {
        bool isDelete = string.Equals(ctx.Action, "delete", StringComparison.OrdinalIgnoreCase);
        if (!isDelete)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        bool isManager = ctx.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(isManager
            ? RuleResult.Passed()
            : RuleResult.Failure($"User '{ctx.UserId}' lacks the 'manager' role required to delete '{ctx.Resource}'."));
    }
}
