namespace MultiTenant.Api.Models;

public sealed class PricingRequest
{
    public decimal BasePrice { get; set; }
    public int SeatCount { get; set; }
    public bool AnnualCommitment { get; set; }
}
