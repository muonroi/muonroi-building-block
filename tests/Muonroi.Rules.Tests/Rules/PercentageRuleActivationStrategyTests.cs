namespace Muonroi.Rules.Tests.Rules;

public class PercentageRuleActivationStrategyTests
{
    [Fact]
    public void Constructor_NullProvider_ShouldThrow()
    {
        Action act = () => new PercentageRuleActivationStrategy<string>(null!);
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void IsActive_100Percent_ShouldAlwaysReturnTrue()
    {
        // Use a deterministic random that returns 0.5 (always < 100/100 = 1.0)
        var strategy = new PercentageRuleActivationStrategy<string>(
            (_, _) => 100,
            new Random(42));

        var rule = Substitute.For<IRule<string>>();

        // Run multiple times
        for (int i = 0; i < 100; i++)
        {
            strategy.IsActive(rule, "ctx").Should().BeTrue();
        }
    }

    [Fact]
    public void IsActive_0Percent_ShouldAlwaysReturnFalse()
    {
        var strategy = new PercentageRuleActivationStrategy<string>(
            (_, _) => 0,
            new Random(42));

        var rule = Substitute.For<IRule<string>>();

        for (int i = 0; i < 100; i++)
        {
            strategy.IsActive(rule, "ctx").Should().BeFalse();
        }
    }

    [Fact]
    public void IsActive_50Percent_ShouldReturnMixOfTrueAndFalse()
    {
        var strategy = new PercentageRuleActivationStrategy<string>(
            (_, _) => 50,
            new Random(42));

        var rule = Substitute.For<IRule<string>>();

        int trueCount = 0;
        int falseCount = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (strategy.IsActive(rule, "ctx"))
                trueCount++;
            else
                falseCount++;
        }

        trueCount.Should().BeGreaterThan(0);
        falseCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void IsActive_ShouldPassRuleAndContextToProvider()
    {
        IRule<string>? capturedRule = null;
        string? capturedContext = null;

        var strategy = new PercentageRuleActivationStrategy<string>(
            (r, c) =>
            {
                capturedRule = r;
                capturedContext = c;
                return 100;
            },
            new Random(42));

        var rule = Substitute.For<IRule<string>>();
        strategy.IsActive(rule, "myctx");

        capturedRule.Should().Be(rule);
        capturedContext.Should().Be("myctx");
    }
}
