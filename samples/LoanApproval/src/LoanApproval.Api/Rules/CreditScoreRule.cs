namespace LoanApproval.Api.Rules;

[RuleGroup("loan-approval")]
public sealed class CreditScoreRule : IRule<LoanApplication>
{
    public string Code => "CREDIT_SCORE";
    public int Order => 0;

    public Task<RuleResult> EvaluateAsync(LoanApplication context, FactBag facts, CancellationToken ct)
    {
        bool eligible = HasMinimumCreditScore(context.CreditScore);
        facts.Set("creditScoreEligible", eligible);
        facts.Set("creditScore", context.CreditScore);

        return Task.FromResult(
            eligible
                ? RuleResult.Passed()
                : RuleResult.Failure("Credit score must be at least 650."));
    }

    [MExtractAsRule("CREDIT_SCORE", Order = 0)]
    private static bool HasMinimumCreditScore(int creditScore)
    {
        return creditScore >= 650;
    }
}
