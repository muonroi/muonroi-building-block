using Muonroi.RuleEngine.Abstractions;
using RuleSourceGen.Api.Models;

namespace RuleSourceGen.Api.Rules;

/// <summary>
/// Discount calculation rules for the sample API.
/// </summary>
public sealed class DiscountRules
{
    /// <summary>Computes the premium customer discount rate.</summary>
    /// <param name="context">Discount evaluation input.</param>
    [MExtractAsRule("DISCOUNT_PREMIUM", Order = 1, DependsOn = ["DISCOUNT_VALIDATE"])]
    public decimal ApplyPremiumDiscount(DiscountRequest context)
    {
        if (!string.Equals(context.CustomerType, "premium", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        return ResolvePremiumRate(context);
    }

    /// <summary>Computes the loyalty-based discount rate.</summary>
    /// <param name="context">Discount evaluation input.</param>
    [MExtractAsRule("DISCOUNT_LOYALTY", Order = 2, DependsOn = ["DISCOUNT_VALIDATE"])]
    public decimal ApplyLoyaltyDiscount(DiscountRequest context)
    {
        return context.LoyaltyYears >= 5 ? 0.05m : 0m;
    }

    /// <summary>Computes the seasonal discount rate.</summary>
    /// <param name="context">Discount evaluation input.</param>
    [MExtractAsRule("DISCOUNT_SEASONAL", Order = 3, DependsOn = ["DISCOUNT_VALIDATE"])]
    public decimal ApplySeasonalDiscount(DiscountRequest context)
    {
        return context.IsBlackFriday ? 0.05m : 0m;
    }

    private static decimal ResolvePremiumRate(DiscountRequest context)
    {
        return context.Subtotal >= 500m ? 0.15m : 0.10m;
    }
}
