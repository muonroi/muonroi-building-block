using Quickstart.RuleEngine.Core.Api.Models;
using Muonroi.RuleEngine.Abstractions;

namespace Quickstart.RuleEngine.Core.Api.Rules;

public class PremiumDiscountRule : IRule<OrderContext>, ICompensatableRule<OrderContext>
{
    public string Code => "PREMIUM_DISCOUNT";
    public int Order => 20;
    
    public IReadOnlyList<string> DependsOn => new[] { "MIN_ORDER_VALUE" };

    public RuleType Type => RuleType.Business;

    public Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct)
    {
        if (ctx.IsPremiumCustomer && ctx.TotalAmount >= 100.0m)
        {
            // Add a fact to show we applied discount
            facts.Set("DiscountApplied", true);
        }
        
        return Task.FromResult(RuleResult.Passed());
    }

    public Task ExecuteAsync(OrderContext context, CancellationToken cancellationToken = default)
    {
        // Business rule actually modifies state during execution
        if (context.IsPremiumCustomer && context.TotalAmount >= 100.0m)
        {
            context.TotalAmount *= 0.9m; // 10% discount
        }
        
        return Task.CompletedTask;
    }

    public Task CompensateAsync(OrderContext context, FactBag facts, CancellationToken ct)
    {
        // If execution fails later, we revert the discount
        context.TotalAmount /= 0.9m;
        return Task.CompletedTask;
    }
}
