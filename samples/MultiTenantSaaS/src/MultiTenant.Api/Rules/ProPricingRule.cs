using MultiTenant.Api.Models;
using Muonroi.RuleEngine.Abstractions;

namespace MultiTenant.Api.Rules;

[TenantRuleGroup("pricing", "tenant-pro")]
public sealed class ProPricingRule : IRule<PricingRequest>
{
    public string Code => "PRICING_PRO";

    public Task<RuleResult> EvaluateAsync(PricingRequest context, FactBag facts, CancellationToken ct)
    {
        decimal multiplier = GetProMultiplier(context.SeatCount);
        facts.Set("plan", "Pro");
        facts.Set("multiplier", multiplier);
        facts.Set("features", new[] { "advanced-dashboard", "email-support", "automations" });
        return Task.FromResult(RuleResult.Passed());
    }

    [MExtractAsRule("PRICING_PRO", Order = 0)]
    private static decimal GetProMultiplier(int seatCount)
    {
        return seatCount > 100 ? 0.95m : 1.00m;
    }
}
