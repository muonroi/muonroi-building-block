namespace MultiTenant.Api.Models;

public sealed class PricingDecisionResponse
{
    public string TenantId { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; }
    public decimal AppliedMultiplier { get; set; }
    public IReadOnlyList<string> EnabledFeatures { get; set; } = [];
}
