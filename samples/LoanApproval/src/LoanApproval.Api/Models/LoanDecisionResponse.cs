namespace LoanApproval.Api.Models;

public sealed class LoanDecisionResponse
{
    public bool Approved { get; set; }
    public string Tier { get; set; } = "manual-review";
    public string Reason { get; set; } = string.Empty;
    public decimal DebtToIncomeRatio { get; set; }
    public IReadOnlyDictionary<string, object?> Facts { get; set; } = new Dictionary<string, object?>();
}
