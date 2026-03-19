using LoanApproval.Api.Models;
using Muonroi.RuleEngine.Abstractions;

namespace LoanApproval.Api.Rules;

[RuleGroup("loan-approval")]
public sealed class DebtRatioRule : IRule<LoanApplication>
{
    public string Code => "DEBT_RATIO";
    public int Order => 1;
    public IReadOnlyList<string> DependsOn => ["CREDIT_SCORE"];

    public Task<RuleResult> EvaluateAsync(LoanApplication context, FactBag facts, CancellationToken ct)
    {
        decimal ratio = CalculateDebtToIncomeRatio(context.MonthlyDebt, context.MonthlyIncome);
        bool eligible = ratio <= 0.45m;

        facts.Set("debtToIncomeRatio", decimal.Round(ratio, 4));
        facts.Set("debtRatioEligible", eligible);

        return Task.FromResult(
            eligible
                ? RuleResult.Passed()
                : RuleResult.Failure("Debt-to-income ratio must be <= 0.45."));
    }

    [MExtractAsRule("DEBT_RATIO", Order = 1)]
    private static decimal CalculateDebtToIncomeRatio(decimal monthlyDebt, decimal monthlyIncome)
    {
        if (monthlyIncome <= 0)
        {
            return 1m;
        }

        return monthlyDebt / monthlyIncome;
    }
}
