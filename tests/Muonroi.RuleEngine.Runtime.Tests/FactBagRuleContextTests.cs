using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Adapters;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FactBagRuleContextTests
{
    [Fact]
    public void Constructor_StoresFactBag()
    {
        FactBag bag = new();
        bag.Set("key", "value");

        FactBagRuleContext ctx = new(bag);

        ctx.Facts.Should().BeSameAs(bag);
        ctx.Facts.Get<string>("key").Should().Be("value");
    }

    [Fact]
    public void HaltGroup_DoesNotThrow()
    {
        FactBagRuleContext ctx = new(new FactBag());

        Action act = () => ctx.HaltGroup();

        act.Should().NotThrow();
    }
}
