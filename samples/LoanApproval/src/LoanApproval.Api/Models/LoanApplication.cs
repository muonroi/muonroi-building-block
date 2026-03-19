namespace LoanApproval.Api.Models;

public sealed class LoanApplication
{
    public string ApplicantId { get; set; } = string.Empty;
    public int CreditScore { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyDebt { get; set; }
    public decimal RequestedAmount { get; set; }
    public int EmploymentMonths { get; set; }
}
