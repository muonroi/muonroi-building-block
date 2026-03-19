namespace RuleSourceGen.Api.Models;

public sealed record DiscountResponse
{
    public decimal PremiumDiscountRate { get; init; }
    public decimal LoyaltyDiscountRate { get; init; }
    public decimal SeasonalDiscountRate { get; init; }
    public decimal TotalDiscountRate { get; init; }
    public decimal FinalTotal { get; init; }
}
