namespace Quickstart.RuleEngine.NRules.Api.Models;

/// <summary>
/// Mutable fact inserted into the NRules session. NRules rules match on facts
/// and mutate them in their Then() action.
/// </summary>
public sealed class Order
{
    /// <summary>Order total before discount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Discount rate applied by the rule engine (0 = none).</summary>
    public decimal DiscountRate { get; private set; }

    /// <summary>Net payable after applying <see cref="DiscountRate"/>.</summary>
    public decimal FinalAmount => decimal.Round(Amount * (1m - DiscountRate), 2);

    /// <summary>Applies a discount rate to this order. Called from the rule action.</summary>
    public void ApplyDiscount(decimal rate) => DiscountRate = rate;
}
