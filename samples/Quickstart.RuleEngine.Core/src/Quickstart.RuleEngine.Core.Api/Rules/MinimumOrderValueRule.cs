namespace Quickstart.RuleEngine.Core.Api.Rules;

public class MinimumOrderValueRule : IRule<OrderContext>
{
    public string Code => "MIN_ORDER_VALUE";
    public int Order => 10;
    
    public RuleType Type => RuleType.Validation;

    public Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct)
    {
        if (ctx.TotalAmount < 10.0m)
        {
            return Task.FromResult(RuleResult.Failure(new[] { "Order amount must be at least 10.00" }));
        }
        
        // Add a fact that can be used by subsequent rules
        facts.Set("OriginalAmount", ctx.TotalAmount);
        
        return Task.FromResult(RuleResult.Passed());
    }
}
