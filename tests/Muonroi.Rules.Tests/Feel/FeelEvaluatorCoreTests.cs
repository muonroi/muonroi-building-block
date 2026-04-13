namespace Muonroi.Rules.Tests.Feel;

public class FeelEvaluatorCoreTests
{
    [Theory]
    [InlineData("age >= 18", true)]
    [InlineData("age > 25", false)]
    [InlineData("age <= 25", true)]
    [InlineData("age < 25", false)]
    [InlineData("age = 25", true)]
    public void Evaluate_Comparisons_ShouldReturnCorrectResult(string expression, bool expected)
    {
        var vars = new Dictionary<string, object> { ["age"] = 25.0 };
        bool result = FeelEvaluator.Evaluate(expression, vars);
        result.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_RangeExpression_ShouldReturnTrue()
    {
        var vars = new Dictionary<string, object> { ["x"] = 5.0 };
        bool result = FeelEvaluator.Evaluate("x in [1..10]", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RangeExpression_OutOfRange_ShouldReturnFalse()
    {
        var vars = new Dictionary<string, object> { ["x"] = 15.0 };
        bool result = FeelEvaluator.Evaluate("x in [1..10]", vars);
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NullExpression_ShouldReturnFalse()
    {
        bool result = FeelEvaluator.Evaluate(null!, new Dictionary<string, object>());
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EmptyExpression_ShouldReturnFalse()
    {
        bool result = FeelEvaluator.Evaluate("", new Dictionary<string, object>());
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateValue_Arithmetic_Addition()
    {
        var result = FeelEvaluator.EvaluateValue("10 + 5", new Dictionary<string, object>());
        result.Should().Be(15.0);
    }

    [Fact]
    public void EvaluateValue_Arithmetic_Subtraction()
    {
        var result = FeelEvaluator.EvaluateValue("10 - 3", new Dictionary<string, object>());
        result.Should().Be(7.0);
    }

    [Fact]
    public void EvaluateValue_Arithmetic_Multiplication()
    {
        var result = FeelEvaluator.EvaluateValue("4 * 5", new Dictionary<string, object>());
        result.Should().Be(20.0);
    }

    [Fact]
    public void EvaluateValue_Arithmetic_Division()
    {
        var result = FeelEvaluator.EvaluateValue("20 / 4", new Dictionary<string, object>());
        result.Should().Be(5.0);
    }

    [Fact]
    public void EvaluateValue_TrueLiteral_ShouldReturnTrue()
    {
        var result = FeelEvaluator.EvaluateValue("true", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_FalseLiteral_ShouldReturnFalse()
    {
        var result = FeelEvaluator.EvaluateValue("false", new Dictionary<string, object>());
        result.Should().Be(false);
    }

    [Fact]
    public void EvaluateValue_VariableResolution()
    {
        var vars = new Dictionary<string, object> { ["name"] = "Alice" };
        var result = FeelEvaluator.EvaluateValue("name", vars);
        result.Should().Be("Alice");
    }

    [Fact]
    public void Evaluate_LogicalAnd_ShouldCombine()
    {
        var vars = new Dictionary<string, object> { ["a"] = 5.0, ["b"] = 10.0 };
        bool result = FeelEvaluator.Evaluate("a > 3 and b > 5", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LogicalOr_ShouldCombine()
    {
        var vars = new Dictionary<string, object> { ["a"] = 1.0, ["b"] = 10.0 };
        bool result = FeelEvaluator.Evaluate("a > 3 or b > 5", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_IfThenElse_TrueCondition()
    {
        var vars = new Dictionary<string, object> { ["x"] = 10.0 };
        var result = FeelEvaluator.EvaluateValue("if x > 5 then \"big\" else \"small\"", vars);
        result.Should().Be("big");
    }

    [Fact]
    public void EvaluateValue_IfThenElse_FalseCondition()
    {
        var vars = new Dictionary<string, object> { ["x"] = 2.0 };
        var result = FeelEvaluator.EvaluateValue("if x > 5 then \"big\" else \"small\"", vars);
        result.Should().Be("small");
    }

    [Fact]
    public void EvaluateValue_ListLiteral()
    {
        var result = FeelEvaluator.EvaluateValue("[1, 2, 3]", new Dictionary<string, object>());
        result.Should().BeAssignableTo<IEnumerable<object>>();
    }

    [Fact]
    public void Evaluate_InList_Unquoted_ShouldMatchValues()
    {
        // Legacy regex-based in-list uses unquoted values
        var vars = new Dictionary<string, object> { ["x"] = "a" };
        bool result = FeelEvaluator.Evaluate("x in (a, b, c)", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Abs()
    {
        var result = FeelEvaluator.EvaluateValue("abs(-5)", new Dictionary<string, object>());
        result.Should().Be(5.0);
    }

    [Fact]
    public void EvaluateValue_Negation()
    {
        var vars = new Dictionary<string, object> { ["x"] = 5.0 };
        var result = FeelEvaluator.EvaluateValue("-x", vars);
        result.Should().Be(-5.0);
    }

    [Fact]
    public void Evaluate_NotExpression_ShouldNegate()
    {
        var result = FeelEvaluator.Evaluate("not(false)", new Dictionary<string, object>());
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_NestedParentheses()
    {
        var result = FeelEvaluator.EvaluateValue("(2 + 3) * (4 - 1)", new Dictionary<string, object>());
        result.Should().Be(15.0);
    }

    [Fact]
    public void EvaluateValue_StringLiteral()
    {
        var result = FeelEvaluator.EvaluateValue("\"hello world\"", new Dictionary<string, object>());
        result.Should().Be("hello world");
    }

    [Fact]
    public void EvaluateValue_NumericLiteral()
    {
        var result = FeelEvaluator.EvaluateValue("42", new Dictionary<string, object>());
        result.Should().Be(42.0);
    }

    [Fact]
    public void EvaluateValue_ComparisonChain()
    {
        var vars = new Dictionary<string, object> { ["a"] = 3.0 };
        var result = FeelEvaluator.Evaluate("a >= 1 and a <= 5", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MatchesRegex_ShouldWork()
    {
        var vars = new Dictionary<string, object> { ["name"] = "John123" };
        bool result = FeelEvaluator.Evaluate("name matches \"^[A-Za-z]+\\d+$\"", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_ForExpression()
    {
        var result = FeelEvaluator.EvaluateValue("for i in 1..3 return i * 2", new Dictionary<string, object>());
        result.Should().BeAssignableTo<IEnumerable<object>>();
    }

    [Fact]
    public void EvaluateValue_WhitespaceExpression_ShouldReturnNull()
    {
        var result = FeelEvaluator.EvaluateValue("   ", new Dictionary<string, object>());
        result.Should().BeNull();
    }
}
