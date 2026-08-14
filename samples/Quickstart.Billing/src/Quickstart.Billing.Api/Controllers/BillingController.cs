namespace Quickstart.Billing.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class BillingController(IBillingProvider billingProvider, IUsageAggregator usageAggregator, ITenantQuotaStore store) : ControllerBase
{
    [HttpPost("record-event")]
    public async Task<IActionResult> RecordEvent([FromQuery] string tenantId = "T-ABC", [FromQuery] QuotaType type = QuotaType.PdfRendersPerDay, [FromQuery] long quantity = 5)
    {
        var billableEvent = new BillableEvent(tenantId, type, quantity, DateTimeOffset.UtcNow);
        
        // Record event using the billing provider (record-only handles tracking but not external APIs)
        await billingProvider.RecordAsync(billableEvent);
        
        // Update local quota store directly for demonstration
        await store.RecordUsageAsync(tenantId, type, (int)quantity);
        
        return Ok(new { Message = "Event recorded successfully" });
    }

    [HttpGet("preview-invoice")]
    public async Task<IActionResult> PreviewInvoice([FromQuery] string tenantId = "T-ABC")
    {
        // Sample Pricing Plan
        var rates = new Dictionary<QuotaType, decimal>
        {
            { QuotaType.PdfRendersPerDay, 0.05m },
            { QuotaType.RuleExecutionsPerDay, 0.01m }
        };
        var plan = new PricingPlan(TenantTier.Starter, rates, flatBaseAmount: 10.0m);
        
        // Aggregate usage from the quota store into line items
        var lineItems = await usageAggregator.AggregateAsync(tenantId, plan, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
        
        // Preview invoice (no charge)
        var preview = await billingProvider.PreviewInvoiceAsync(tenantId, lineItems);
        
        return Ok(new { TenantId = tenantId, Total = preview.Sum(li => li.Amount), Items = preview });
    }
}
