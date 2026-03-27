using Muonroi.RuleEngine.Abstractions;
using Quickstart.RuleEngine.Api.Models;

namespace Quickstart.RuleEngine.Api.Rules;

[RuleGroup("order-discount")]
public sealed class HighValueOrderRule : IRule<OrderRequest>
{
    public string Code => "HIGH_VALUE_ORDER";
    public int Order => 0;

    public Task<RuleResult> EvaluateAsync(OrderRequest context, FactBag facts, CancellationToken ct)
    {
        if (context.Amount <= 0)
        {
            return Task.FromResult(RuleResult.Failure("Amount must be greater than zero."));
        }

        bool isHighValueOrder = context.Amount >= 1000m;
        bool isPremiumCustomer = string.Equals(context.CustomerType, "premium", StringComparison.OrdinalIgnoreCase);

        decimal discountRate = 0m;
        if (isHighValueOrder)
        {
            discountRate += 0.10m;
        }

        if (isPremiumCustomer)
        {
            discountRate += 0.05m;
        }

        discountRate = Math.Min(discountRate, 0.20m);

        facts.Set("discountRate", discountRate);
        facts.Set("isHighValueOrder", isHighValueOrder);
        facts.Set("isPremiumCustomer", isPremiumCustomer);
        facts.Set("countryCode", context.CountryCode);

        return Task.FromResult(RuleResult.Passed());
    }
}
