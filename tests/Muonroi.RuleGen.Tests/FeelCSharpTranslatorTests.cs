using Muonroi.RuleGen.Services;
using FluentAssertions;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public class FeelCSharpTranslatorTests
{
    [Fact]
    public void FeelToCSharpCondition_Null_ReturnsTrue()
    {
        FeelCSharpTranslator.FeelToCSharpCondition(null).Should().Be("true");
    }

    [Fact]
    public void FeelToCSharpCondition_Empty_ReturnsTrue()
    {
        FeelCSharpTranslator.FeelToCSharpCondition("  ").Should().Be("true");
    }

    [Theory]
    [InlineData("x and y", "x && y")]
    [InlineData("x or y", "x || y")]
    [InlineData("not x", "! x")]
    [InlineData("x AND y", "x && y")]
    [InlineData("x OR y", "x || y")]
    public void FeelToCSharpCondition_LogicalOperators_Translated(string feel, string expected)
    {
        string result = FeelCSharpTranslator.FeelToCSharpCondition(feel);
        result.Should().Be(expected);
    }

    [Fact]
    public void FeelToCSharpCondition_SingleEquals_BecomesDoubleEquals()
    {
        string result = FeelCSharpTranslator.FeelToCSharpCondition("x = 5");
        result.Should().Contain("==");
    }

    [Fact]
    public void FeelToCSharpCondition_DoubleEquals_NotChanged()
    {
        string result = FeelCSharpTranslator.FeelToCSharpCondition("x == 5");
        result.Should().Contain("==");
    }

    [Fact]
    public void FeelToCSharpCondition_SingleQuotes_BecomesDoubleQuotes()
    {
        string result = FeelCSharpTranslator.FeelToCSharpCondition("status = 'active'");
        result.Should().Contain("\"active\"");
        result.Should().NotContain("'active'");
    }

    [Fact]
    public void FeelToCSharpCondition_DottedPath_ConvertedToCSharpPath()
    {
        string result = FeelCSharpTranslator.FeelToCSharpCondition("order.status = 'active'");
        result.Should().Contain("ctx.Order.Status");
    }

    [Fact]
    public void FeelToCSharpCondition_CtxPrefix_NotDoubled()
    {
        string result = FeelCSharpTranslator.FeelToCSharpCondition("ctx.Order.Status == 'active'");
        result.Should().Contain("ctx.Order.Status");
        result.Should().NotContain("ctx.ctx.");
    }

    [Fact]
    public void ActionToCSharp_Null_ReturnsEmpty()
    {
        FeelCSharpTranslator.ActionToCSharp(null).Should().Be(string.Empty);
    }

    [Fact]
    public void ActionToCSharp_Empty_ReturnsEmpty()
    {
        FeelCSharpTranslator.ActionToCSharp("  ").Should().Be(string.Empty);
    }

    [Fact]
    public void ActionToCSharp_FactAssignment_TranslatedCorrectly()
    {
        string result = FeelCSharpTranslator.ActionToCSharp("facts['discount'] = 0.1");
        result.Should().Contain("facts[\"discount\"]");
        result.Should().Contain("0.1");
        result.Should().EndWith(";");
    }

    [Fact]
    public void ActionToCSharp_UnsupportedAction_ReturnsComment()
    {
        string result = FeelCSharpTranslator.ActionToCSharp("doSomething()");
        result.Should().StartWith("// Unsupported action expression:");
    }

    [Fact]
    public void CSharpToFeel_Null_ReturnsTrue()
    {
        FeelCSharpTranslator.CSharpToFeel(null).Should().Be("true");
    }

    [Fact]
    public void CSharpToFeel_Empty_ReturnsTrue()
    {
        FeelCSharpTranslator.CSharpToFeel("  ").Should().Be("true");
    }

    [Fact]
    public void CSharpToFeel_AndOperator_Converted()
    {
        string result = FeelCSharpTranslator.CSharpToFeel("x && y");
        result.Should().Contain(" and ");
    }

    [Fact]
    public void CSharpToFeel_OrOperator_Converted()
    {
        string result = FeelCSharpTranslator.CSharpToFeel("x || y");
        result.Should().Contain(" or ");
    }

    [Fact]
    public void CSharpToFeel_DoubleEquals_ConvertedToSingleEquals()
    {
        string result = FeelCSharpTranslator.CSharpToFeel("x == 5");
        result.Should().Contain(" = ");
    }

    [Fact]
    public void CSharpToFeel_CtxPath_ConvertedToCamelCase()
    {
        string result = FeelCSharpTranslator.CSharpToFeel("ctx.Order.Status");
        result.Should().Contain("order.status");
    }

    [Fact]
    public void RoundTrip_FeelToCSharpAndBack()
    {
        string original = "x and y";
        string csharp = FeelCSharpTranslator.FeelToCSharpCondition(original);
        string backToFeel = FeelCSharpTranslator.CSharpToFeel(csharp);
        backToFeel.Should().Contain("and");
    }
}
