namespace Quickstart.RuleEngine.SourceGenerators.Api.Rules;

public partial class UserValidationRules
{
    [MExtractAsRule("AGE_VALIDATION")]
    public async Task<RuleResult> ValidateAgeAsync(ValidationContext ctx, FactBag facts, CancellationToken ct)
    {
        if (ctx.Age < 18)
        {
            return RuleResult.Failure(new[] { "User must be at least 18 years old" });
        }
        
        facts.Set("IsAdult", true);
        return RuleResult.Passed();
    }
    
    [MExtractAsRule("TEXT_VALIDATION")]
    public Task<RuleResult> ValidateTextAsync(ValidationContext ctx, FactBag facts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.InputText))
        {
            return Task.FromResult(RuleResult.Failure(new[] { "Input text cannot be empty" }));
        }
        
        return Task.FromResult(RuleResult.Passed());
    }
}
