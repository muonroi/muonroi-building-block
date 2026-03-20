using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Testing;
using RuleSourceGen.Api.Models;
using RuleSourceGen.Api.Rules;

namespace RuleSourceGen.Api.Tests;

public class GeneratedRuleSampleTests
{
    [Fact]
    public async Task GeneratedRules_ProduceExpectedDiscountFacts()
    {
        IRule<DiscountRequest>[] rules =
        [
            new DISCOUNT_VALIDATERule(),
            new DISCOUNT_PREMIUMRule(),
            new DISCOUNT_LOYALTYRule(),
            new DISCOUNT_SEASONALRule()
        ];

        MRuleOrchestratorSpy<DiscountRequest> spy = new(rules);

        FactBag facts = await spy.ExecuteAsync(new DiscountRequest
        {
            CustomerType = "premium",
            Subtotal = 600m,
            LoyaltyYears = 6,
            IsBlackFriday = true
        });

        decimal totalRate = facts.Get<decimal>("DISCOUNT_PREMIUM:result")
                            + facts.Get<decimal>("DISCOUNT_LOYALTY:result")
                            + facts.Get<decimal>("DISCOUNT_SEASONAL:result");

        Assert.Equal(0.25m, totalRate);
        Assert.Equal(
            ["DISCOUNT_VALIDATE", "DISCOUNT_PREMIUM", "DISCOUNT_LOYALTY", "DISCOUNT_SEASONAL"],
            (IEnumerable<string>)[.. spy.ExecutionRecords.Select(x => x.RuleCode)]);
    }

    [Fact]
    public async Task ValidationRule_FailsForMissingCustomerType()
    {
        IRule<DiscountRequest>[] rules = [new DISCOUNT_VALIDATERule()];
        MRuleOrchestratorSpy<DiscountRequest> spy = new(rules);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => spy.ExecuteAsync(new DiscountRequest
        {
            Subtotal = 100m
        }));

        Assert.Contains("CustomerType is required", ex.Message);
    }
}
