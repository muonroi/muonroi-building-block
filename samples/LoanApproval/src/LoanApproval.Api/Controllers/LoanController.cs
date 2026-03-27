using LoanApproval.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;

namespace LoanApproval.Api.Controllers;

[ApiController]
[Route("api/loans")]
public sealed class LoanController(RuleOrchestrator<LoanApplication> orchestrator) : ControllerBase
{
    private readonly RuleOrchestrator<LoanApplication> _orchestrator = orchestrator;

    [HttpPost]
    public async Task<ActionResult<LoanDecisionResponse>> EvaluateAsync(
        [FromBody] LoanApplication request,
        CancellationToken cancellationToken)
    {
        try
        {
            FactBag facts = await _orchestrator.ExecuteAsync(request, cancellationToken: cancellationToken);
            decimal ratio = facts.Get<decimal>("debtToIncomeRatio");
            string tier = ResolveTier(request.CreditScore, ratio);
            bool approved = tier != "manual-review" && request.EmploymentMonths >= 6;

            return Ok(new LoanDecisionResponse
            {
                Approved = approved,
                Tier = tier,
                Reason = approved
                    ? "Loan auto-approved by rule workflow."
                    : "Manual review required by policy thresholds.",
                DebtToIncomeRatio = ratio,
                Facts = facts.AsReadOnly()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new LoanDecisionResponse
            {
                Approved = false,
                Tier = "manual-review",
                Reason = ex.Message,
                DebtToIncomeRatio = request.MonthlyIncome <= 0 ? 1m : decimal.Round(request.MonthlyDebt / request.MonthlyIncome, 4)
            });
        }
    }

    private static string ResolveTier(int creditScore, decimal debtToIncomeRatio)
    {
        if (creditScore >= 760 && debtToIncomeRatio <= 0.30m)
        {
            return "premium";
        }

        if (creditScore >= 700 && debtToIncomeRatio <= 0.40m)
        {
            return "standard";
        }

        return "manual-review";
    }
}
