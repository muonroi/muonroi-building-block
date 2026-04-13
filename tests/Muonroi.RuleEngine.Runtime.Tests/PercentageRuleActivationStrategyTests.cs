using Muonroi.Core.Abstractions.Exceptions;
using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using NSubstitute;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class PercentageRuleActivationStrategyTests
{
    [Fact]
    public void IsActive_100Percent_AlwaysReturnsTrue()
    {
        PercentageRuleActivationStrategy<string> sut = new(
            (_, _) => 100,
            new Random(42));

        IRule<string> rule = Substitute.For<IRule<string>>();

        bool result = sut.IsActive(rule, "context");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsActive_0Percent_AlwaysReturnsFalse()
    {
        PercentageRuleActivationStrategy<string> sut = new(
            (_, _) => 0,
            new Random(42));

        IRule<string> rule = Substitute.For<IRule<string>>();

        bool result = sut.IsActive(rule, "context");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_50Percent_ReturnsMixedResults()
    {
        Random seeded = new(12345);
        PercentageRuleActivationStrategy<string> sut = new(
            (_, _) => 50,
            seeded);

        IRule<string> rule = Substitute.For<IRule<string>>();

        int trueCount = 0;
        for (int i = 0; i < 100; i++)
        {
            if (sut.IsActive(rule, "ctx"))
                trueCount++;
        }

        trueCount.Should().BeGreaterThan(20);
        trueCount.Should().BeLessThan(80);
    }

    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        Action act = () => new PercentageRuleActivationStrategy<string>(null!);

        act.Should().Throw<MArgumentException>();
    }
}
