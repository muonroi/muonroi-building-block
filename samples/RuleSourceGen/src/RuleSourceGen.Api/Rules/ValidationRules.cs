using Muonroi.RuleEngine.Abstractions;
using RuleSourceGen.Api.Models;

namespace RuleSourceGen.Api.Rules;

/// <summary>
/// Input validation rules for discount evaluation.
/// </summary>
public sealed class ValidationRules
{
    /// <summary>Validates the discount request and returns rule status.</summary>
    /// <param name="context">Discount evaluation input.</param>
    [MExtractAsRule("DISCOUNT_VALIDATE", Order = 0)]
    public RuleResult Validate(DiscountRequest context)
    {
        if (string.IsNullOrWhiteSpace(context.CustomerType))
        {
            return RuleResult.Failure("CustomerType is required.");
        }

        if (context.Subtotal <= 0m)
        {
            return RuleResult.Failure("Subtotal must be greater than zero.");
        }

        return RuleResult.Passed();
    }
}
