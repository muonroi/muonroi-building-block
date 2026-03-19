namespace RuleSourceGen.Api.Models;

public sealed record DiscountRequest
{
    public string CustomerType { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public int LoyaltyYears { get; init; }
    public bool IsBlackFriday { get; init; }
}
