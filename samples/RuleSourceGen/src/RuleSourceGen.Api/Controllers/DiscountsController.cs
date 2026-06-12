using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;
using RuleSourceGen.Api.Models;

namespace RuleSourceGen.Api.Controllers;

/// <summary>
/// Endpoints for discount rule evaluation.
/// </summary>
[ApiController]
[Route("api/discounts")]
public sealed class DiscountsController(RuleOrchestrator<DiscountRequest> orchestrator) : ControllerBase
{
    /// <summary>
    /// Evaluates discount rules and returns the computed rates.
    /// </summary>
    /// <param name="request">Discount input payload.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("evaluate")]
    public async Task<ActionResult<DiscountResponse>> Evaluate(
        [FromBody] DiscountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            MGuard.NotNull(request, nameof(request));
            FactBag facts = await orchestrator.ExecuteAsync(request, cancellationToken: cancellationToken);
            decimal premium = facts.Get<decimal>("DISCOUNT_PREMIUM:result");
            decimal loyalty = facts.Get<decimal>("DISCOUNT_LOYALTY:result");
            decimal seasonal = facts.Get<decimal>("DISCOUNT_SEASONAL:result");
            decimal totalRate = Math.Min(0.30m, premium + loyalty + seasonal);

            return Ok(new DiscountResponse
            {
                PremiumDiscountRate = premium,
                LoyaltyDiscountRate = loyalty,
                SeasonalDiscountRate = seasonal,
                TotalDiscountRate = totalRate,
                FinalTotal = request.Subtotal * (1m - totalRate)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
