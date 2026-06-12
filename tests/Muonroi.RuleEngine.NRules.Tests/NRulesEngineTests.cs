using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.RuleEngine.NRules.Tests;

public class NRulesEngineTests
{
    public sealed class Order
    {
        public int Amount { get; set; }
        public int Discount { get; private set; }

        public void SetDiscount(int value)
        {
            Discount = value;
        }
    }

    [Rule("DiscountRule", "1.0")]
    public sealed class DiscountRuleV1 : Rule
    {
        public override void Define()
        {
            Order order = null!;
            When()
                .Match(() => order, o => o.Amount > 100);
            Then()
                .Do(ctx => order.SetDiscount(10));
        }
    }

    [Rule("DiscountRule", "2.0")]
    public sealed class DiscountRuleV2 : Rule
    {
        public override void Define()
        {
            Order order = null!;
            When()
                .Match(() => order, o => o.Amount > 100);
            Then()
                .Do(ctx => order.SetDiscount(20));
        }
    }

    [Fact]
    public void Fire_UsesConfiguredVersion()
    {
        RuleOptions options = new();
        options.Rules["DiscountRule"] = new RuleConfig { Version = "2.0" };
        NRulesEngine engine = new([typeof(DiscountRuleV1).Assembly], Options.Create(options));
        Order order = new()
        {
            Amount = 200
        };
        engine.Fire(order);
        Assert.Equal(20, order.Discount);
    }

    [Fact]
    public void Constructor_ThrowsOnConflictingRules()
    {
        RuleOptions options = new(); // no version specified -> both rules enabled
        Assert.Throws<MInternalException>(() =>
            new NRulesEngine([typeof(DiscountRuleV1).Assembly], Options.Create(options)));
    }
}