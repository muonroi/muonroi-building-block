using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Writers;
using FluentAssertions;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public class TestScaffoldWriterTests
{
    [Fact]
    public void Render_GeneratesValidTestClass()
    {
        DiscoveredRuleType ruleType = new("ValidateOrderRule", "OrderContext");

        string result = TestScaffoldWriter.Render(ruleType, "MyApp.Tests");

        result.Should().Contain("namespace MyApp.Tests;");
        result.Should().Contain("public class ValidateOrderRuleTests");
        result.Should().Contain("ValidateOrderRule _rule");
        result.Should().Contain("[Fact]");
        result.Should().Contain("EvaluateAsync");
        result.Should().Contain("FactBag");
        result.Should().Contain("Assert.NotNull(result)");
    }

    [Fact]
    public void Render_IncludesUsings()
    {
        DiscoveredRuleType ruleType = new("MyRule", "MyCtx");
        string result = TestScaffoldWriter.Render(ruleType, "Tests");

        result.Should().Contain("using Xunit;");
        result.Should().Contain("using Muonroi.RuleEngine.Abstractions;");
        result.Should().Contain("using System.Threading;");
    }
}
