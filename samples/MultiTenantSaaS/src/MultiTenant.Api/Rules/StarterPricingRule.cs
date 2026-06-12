using MultiTenant.Api.Models;
using Muonroi.RuleEngine.Abstractions;

namespace MultiTenant.Api.Rules;

[TenantRuleGroup("pricing", "tenant-starter")]
public sealed class StarterPricingRule : IRule<PricingRequest>
{
    public string Code => "PRICING_STARTER";

    public Task<RuleResult> EvaluateAsync(PricingRequest context, FactBag facts, CancellationToken ct)
    {
        decimal multiplier = GetStarterMultiplier(context.SeatCount);
        facts.Set("plan", "Starter");
        facts.Set("multiplier", multiplier);
        facts.Set("features", new[] { "basic-dashboard", "community-support" });
        return Task.FromResult(RuleResult.Passed());
    }

    [MExtractAsRule("PRICING_STARTER", Order = 0)]
    private static decimal GetStarterMultiplier(int seatCount)
    {
        return seatCount > 25 ? 1.20m : 1.15m;
    }
}
