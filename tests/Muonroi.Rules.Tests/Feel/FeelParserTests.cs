namespace Muonroi.Rules.Tests.Feel;

public class FeelParserTests
{
    [Fact]
    public void Parse_SimpleComparison_ReturnsCorrectResult()
    {
        var vars = new Dictionary<string, object> { ["x"] = 10.0 };
        object? result = FeelParser.Parse("x > 5", vars);
        result.Should().Be(true);
    }

    [Fact]
    public void Parse_NullExpression_ReturnsNull()
    {
        object? result = FeelParser.Parse(null!, new Dictionary<string, object>());
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyExpression_ReturnsNull()
    {
        object? result = FeelParser.Parse("", new Dictionary<string, object>());
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_ArithmeticExpression_ReturnsNumericResult()
    {
        var vars = new Dictionary<string, object> { ["a"] = 3.0, ["b"] = 4.0 };
        object? result = FeelParser.Parse("a + b", vars);
        result.Should().Be(7.0);
    }

    [Fact]
    public void Parse_StringVariable_ReturnsStringValue()
    {
        var vars = new Dictionary<string, object> { ["name"] = "test" };
        object? result = FeelParser.Parse("name", vars);
        result.Should().Be("test");
    }
}
