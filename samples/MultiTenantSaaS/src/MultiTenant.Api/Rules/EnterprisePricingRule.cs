namespace MultiTenant.Api.Rules;

[TenantRuleGroup("pricing", "tenant-enterprise")]
public sealed class EnterprisePricingRule : IRule<PricingRequest>
{
    public string Code => "PRICING_ENTERPRISE";

    public Task<RuleResult> EvaluateAsync(PricingRequest context, FactBag facts, CancellationToken ct)
    {
        decimal multiplier = GetEnterpriseMultiplier(context.SeatCount);
        facts.Set("plan", "Enterprise");
        facts.Set("multiplier", multiplier);
        facts.Set("features", new[] { "sso", "canary-rollout", "audit-trail", "priority-support" });
        return Task.FromResult(RuleResult.Passed());
    }

    [MExtractAsRule("PRICING_ENTERPRISE", Order = 0)]
    private static decimal GetEnterpriseMultiplier(int seatCount)
    {
        return seatCount > 200 ? 0.80m : 0.85m;
    }
}
