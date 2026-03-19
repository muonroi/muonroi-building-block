using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;
using Quickstart.RuleEngine.Api.Models;

namespace Quickstart.RuleEngine.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(RuleOrchestrator<OrderRequest> orchestrator) : ControllerBase
{
    private readonly RuleOrchestrator<OrderRequest> _orchestrator = orchestrator;

    [HttpPost("evaluate")]
    public async Task<ActionResult<OrderDecisionResponse>> EvaluateAsync(
        [FromBody] OrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            FactBag facts = await _orchestrator.ExecuteAsync(request, cancellationToken: cancellationToken);
            decimal discountRate = facts.Get<decimal>("discountRate");
            decimal finalAmount = decimal.Round(request.Amount * (1m - discountRate), 2);

            return Ok(new OrderDecisionResponse
            {
                OriginalAmount = request.Amount,
                DiscountRate = discountRate,
                FinalAmount = finalAmount,
                Decision = discountRate > 0 ? "discount-applied" : "no-discount",
                Facts = facts.AsReadOnly()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new OrderDecisionResponse
            {
                OriginalAmount = request.Amount,
                FinalAmount = request.Amount,
                Decision = ex.Message
            });
        }
    }
}
