using Microsoft.AspNetCore.Mvc;
using Quickstart.RuleEngine.Core.Api.Models;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;

namespace Quickstart.RuleEngine.Core.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly RuleOrchestrator<OrderContext> _ruleOrchestrator;

    public OrderController(RuleOrchestrator<OrderContext> ruleOrchestrator)
    {
        _ruleOrchestrator = ruleOrchestrator;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessOrder([FromBody] OrderContext order, CancellationToken ct)
    {
        // Execute the rule engine pipeline for this order context
        OrchestratorResult result = await _ruleOrchestrator.ExecuteAsync(order, HookPoint.BeforeRule, ct);
        
        if (!result.Success)
        {
            return BadRequest(new 
            {
                Error = "Order processing failed due to rule violations",
                Details = result.Errors
            });
        }

        return Ok(new
        {
            Status = "Processed Successfully",
            FinalAmount = order.TotalAmount,
            OriginalAmount = result.Facts.Get<decimal>("OriginalAmount"),
            DiscountApplied = result.Facts.Get<bool>("DiscountApplied"),
            ExecutedRules = result.ExecutedRuleCodes
        });
    }
}
