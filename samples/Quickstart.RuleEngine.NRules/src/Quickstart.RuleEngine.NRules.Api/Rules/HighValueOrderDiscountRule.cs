namespace Quickstart.RuleEngine.NRules.Api.Rules;

/// <summary>
/// NRules rule demonstrating the Muonroi NRules integration.
///
/// - Derives from NRules' fluent <see cref="Rule"/> base (When / Then DSL).
/// - The Muonroi [Rule(name, version)] attribute lets RuleOptions enable/disable
///   or pin a specific version via the "NRules:Rules" config section.
///
/// When an Order's Amount exceeds 1000 the rule sets a 10% discount on the order.
/// </summary>
[Rule("HighValueOrderDiscount", "1.0")]
public sealed class HighValueOrderDiscountRule : Rule
{
    public override void Define()
    {
        Order order = null!;

        When()
            .Match(() => order, o => o.Amount > 1000m);

        Then()
            .Do(_ => order.ApplyDiscount(0.10m));
    }
}
