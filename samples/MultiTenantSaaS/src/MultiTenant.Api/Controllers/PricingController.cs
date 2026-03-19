using Microsoft.AspNetCore.Mvc;
using MultiTenant.Api.Models;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;

namespace MultiTenant.Api.Controllers;

[ApiController]
[Route("api/pricing")]
public sealed class PricingController(IServiceProvider services) : ControllerBase
{
    private readonly IServiceProvider _services = services;

    [HttpPost("{tenantId}")]
    public async Task<ActionResult<PricingDecisionResponse>> EvaluateAsync(
        string tenantId,
        [FromBody] PricingRequest request,
        CancellationToken cancellationToken)
    {
        string key = $"pricing:{tenantId}";
        RuleOrchestrator<PricingRequest>? orchestrator = _services.GetKeyedService<RuleOrchestrator<PricingRequest>>(key);
        if (orchestrator is null)
        {
            return NotFound(new
            {
                message = "Unknown tenant. Use tenant-starter, tenant-pro, or tenant-enterprise."
            });
        }

        FactBag facts = await orchestrator.ExecuteAsync(request, cancellationToken: cancellationToken);

        decimal multiplier = facts.Get<decimal>("multiplier");
        if (request.AnnualCommitment)
        {
            multiplier = Math.Max(0.70m, multiplier - 0.05m);
        }

        decimal finalPrice = decimal.Round(request.BasePrice * request.SeatCount * multiplier, 2);
        string[] features = facts.Get<string[]>("features") ?? [];

        return Ok(new PricingDecisionResponse
        {
            TenantId = tenantId,
            Plan = facts.Get<string>("plan") ?? "Unknown",
            AppliedMultiplier = multiplier,
            FinalPrice = finalPrice,
            EnabledFeatures = features
        });
    }
}
