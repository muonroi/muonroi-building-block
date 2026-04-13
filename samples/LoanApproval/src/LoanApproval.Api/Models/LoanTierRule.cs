namespace LoanApproval.Api.Models;

public sealed class LoanTierRule
{
    public string Tier { get; set; } = string.Empty;
    public int MinCreditScore { get; set; }
    public decimal MaxDebtToIncome { get; set; }
}
