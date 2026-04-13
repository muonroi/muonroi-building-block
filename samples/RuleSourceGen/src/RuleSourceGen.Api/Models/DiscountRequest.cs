namespace RuleSourceGen.Api.Models;

/// <summary>
/// Input payload for discount rule evaluation.
/// </summary>
public sealed record DiscountRequest
{
    /// <summary>Customer tier or category.</summary>
    public string CustomerType { get; init; } = string.Empty;
    /// <summary>Order subtotal before discounts.</summary>
    public decimal Subtotal { get; init; }
    /// <summary>Number of loyalty years.</summary>
    public int LoyaltyYears { get; init; }
    /// <summary>Whether the order is on Black Friday.</summary>
    public bool IsBlackFriday { get; init; }
}
