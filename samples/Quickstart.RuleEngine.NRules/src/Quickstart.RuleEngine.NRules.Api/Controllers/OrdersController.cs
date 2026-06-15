using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.NRules;
using Quickstart.RuleEngine.NRules.Api.Models;

namespace Quickstart.RuleEngine.NRules.Api.Controllers;

/// <summary>
/// Demonstrates firing the singleton NRulesEngine directly.
///
/// NRulesEngine.Fire(params object[] facts) inserts facts into a fresh NRules
/// session and runs the agenda. Rules mutate the facts in place, so the
/// discounted Order is observable after the call returns.
/// </summary>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(NRulesEngine engine) : ControllerBase
{
    /// <summary>
    /// Evaluates an order amount against the compiled NRules rules.
    /// POST /api/orders/evaluate  body: { "amount": 1500 }
    /// </summary>
    [HttpPost("evaluate")]
    public IActionResult Evaluate([FromBody] EvaluateOrderRequest request)
    {
        Order order = new() { Amount = request.Amount };

        // Insert the fact and fire the agenda. The HighValueOrderDiscountRule
        // applies a 10% discount when Amount > 1000.
        engine.Fire(order);

        return Ok(new
        {
            originalAmount = order.Amount,
            discountRate = order.DiscountRate,
            finalAmount = order.FinalAmount,
            decision = order.DiscountRate > 0 ? "discount-applied" : "no-discount"
        });
    }
}

/// <summary>Request body for order evaluation.</summary>
public sealed record EvaluateOrderRequest
{
    /// <summary>Order total amount.</summary>
    public decimal Amount { get; init; }
}
