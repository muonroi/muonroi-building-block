namespace RuleSourceGen.Api.Models;

/// <summary>
/// Output payload containing computed discount rates.
/// </summary>
public sealed record DiscountResponse
{
    /// <summary>Discount rate for premium customers.</summary>
    public decimal PremiumDiscountRate { get; init; }
    /// <summary>Discount rate based on loyalty.</summary>
    public decimal LoyaltyDiscountRate { get; init; }
    /// <summary>Discount rate based on seasonal promotion.</summary>
    public decimal SeasonalDiscountRate { get; init; }
    /// <summary>Combined discount rate applied.</summary>
    public decimal TotalDiscountRate { get; init; }
    /// <summary>Final total after discounts.</summary>
    public decimal FinalTotal { get; init; }
}
