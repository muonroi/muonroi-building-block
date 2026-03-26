using Muonroi.RuleEngine.Abstractions;
using RuleSourceGen.Api.Models;
using RuleSourceGen.Api.Rules;

namespace RuleSourceGen.Api.Tests;

public class GeneratedRuleSampleTests
{
    [Fact]
    public async Task GeneratedRules_ProduceExpectedDiscountFacts()
    {
        DiscountRequest request = new()
        {
            CustomerType = "premium",
            Subtotal = 600m,
            LoyaltyYears = 6,
            IsBlackFriday = true
        };

        FactBag facts = new();

        // Execute rules in dependency order (VALIDATE → PREMIUM → LOYALTY → SEASONAL)
        IRule<DiscountRequest>[] rules =
        [
            new DISCOUNT_VALIDATERule(),
            new DISCOUNT_PREMIUMRule(),
            new DISCOUNT_LOYALTYRule(),
            new DISCOUNT_SEASONALRule()
        ];

        List<string> executedCodes = [];
        foreach (IRule<DiscountRequest> rule in rules)
        {
            RuleResult result = await rule.EvaluateAsync(request, facts, CancellationToken.None);
            Assert.True(result.IsSuccess, $"Rule {rule.Code} failed: {string.Join(", ", result.Errors)}");
            executedCodes.Add(rule.Code);
        }

        decimal totalRate = facts.Get<decimal>("DISCOUNT_PREMIUM:result")
                            + facts.Get<decimal>("DISCOUNT_LOYALTY:result")
                            + facts.Get<decimal>("DISCOUNT_SEASONAL:result");

        Assert.Equal(0.25m, totalRate);
        Assert.Equal(
            ["DISCOUNT_VALIDATE", "DISCOUNT_PREMIUM", "DISCOUNT_LOYALTY", "DISCOUNT_SEASONAL"],
            (IEnumerable<string>)executedCodes);
    }

    [Fact]
    public async Task ValidationRule_FailsForMissingCustomerType()
    {
        DISCOUNT_VALIDATERule rule = new();
        FactBag facts = new();

        RuleResult result = await rule.EvaluateAsync(new DiscountRequest
        {
            Subtotal = 100m
        }, facts, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("CustomerType is required"));
    }
}
