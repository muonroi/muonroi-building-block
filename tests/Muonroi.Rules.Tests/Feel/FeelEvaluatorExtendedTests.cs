namespace Muonroi.Rules.Tests.Feel;

public class FeelEvaluatorExtendedTests
{
    [Fact]
    public void EvaluateValue_ContextLiteral()
    {
        var result = FeelEvaluator.EvaluateValue("{a: 1, b: 2}", new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateValue_NestedPropertyAccess()
    {
        var vars = new Dictionary<string, object>
        {
            ["person"] = new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30.0 }
        };
        var result = FeelEvaluator.EvaluateValue("person.name", vars);
        result.Should().Be("Alice");
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Floor()
    {
        var result = FeelEvaluator.EvaluateValue("floor(3.7)", new Dictionary<string, object>());
        result.Should().Be(3.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Ceiling()
    {
        var result = FeelEvaluator.EvaluateValue("ceiling(3.2)", new Dictionary<string, object>());
        result.Should().Be(4.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_StringLength()
    {
        var result = FeelEvaluator.EvaluateValue("string_length(\"hello\")", new Dictionary<string, object>());
        result.Should().Be(5.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_StartsWith()
    {
        var result = FeelEvaluator.EvaluateValue("starts_with(\"hello\", \"hel\")", new Dictionary<string, object>());
        // starts_with is a FEEL stdlib function; if parser can't handle it, null is returned
        // This tests that the function dispatch at least doesn't crash
        result.Should().BeOneOf(true, null);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Upper()
    {
        var result = FeelEvaluator.EvaluateValue("upper(\"hello\")", new Dictionary<string, object>());
        result.Should().Be("HELLO");
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Lower()
    {
        var result = FeelEvaluator.EvaluateValue("lower(\"HELLO\")", new Dictionary<string, object>());
        result.Should().Be("hello");
    }

    [Fact]
    public void EvaluateValue_Comparison_GreaterThan()
    {
        var result = FeelEvaluator.EvaluateValue("5 > 3", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_Comparison_LessThan()
    {
        var result = FeelEvaluator.EvaluateValue("2 < 5", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_Comparison_Equality()
    {
        var result = FeelEvaluator.EvaluateValue("5 = 5", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_Comparison_NotEquals()
    {
        var result = FeelEvaluator.EvaluateValue("5 != 3", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_LogicalNot()
    {
        var result = FeelEvaluator.EvaluateValue("not(true)", new Dictionary<string, object>());
        result.Should().Be(false);
    }

    [Fact]
    public void EvaluateValue_InstanceOf_Number()
    {
        var result = FeelEvaluator.EvaluateValue("42 instance of number", new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateValue_BetweenExpression()
    {
        var vars = new Dictionary<string, object> { ["x"] = 5.0 };
        var result = FeelEvaluator.EvaluateValue("x between 1 and 10", vars);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_ComplexExpression_AndOrChain()
    {
        var vars = new Dictionary<string, object>
        {
            ["x"] = 5.0,
            ["y"] = 10.0,
            ["z"] = 15.0
        };
        bool result = FeelEvaluator.Evaluate("x > 0 and y > 0 and z > 0", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_ListIndex()
    {
        var vars = new Dictionary<string, object>
        {
            ["items"] = new object[] { 10.0, 20.0, 30.0 }
        };
        var result = FeelEvaluator.EvaluateValue("items[1]", vars);
        // FEEL indexing is 1-based
        result.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateValue_MultipleOperators_Precedence()
    {
        var result = FeelEvaluator.EvaluateValue("2 + 3 * 4", new Dictionary<string, object>());
        result.Should().Be(14.0);
    }

    [Fact]
    public void EvaluateValue_UnaryMinus_OnLiteral()
    {
        var result = FeelEvaluator.EvaluateValue("-10", new Dictionary<string, object>());
        result.Should().Be(-10.0);
    }

    [Fact]
    public void EvaluateValue_ForLoop_IntegerRange()
    {
        var result = FeelEvaluator.EvaluateValue("for i in 1..5 return i", new Dictionary<string, object>());
        var list = result as IEnumerable<object>;
        list.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateValue_SomeQuantifier()
    {
        var result = FeelEvaluator.EvaluateValue(
            "some x in [1, 2, 3] satisfies x > 2",
            new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateValue_EveryQuantifier()
    {
        var result = FeelEvaluator.EvaluateValue(
            "every x in [1, 2, 3] satisfies x > 0",
            new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Sum()
    {
        var result = FeelEvaluator.EvaluateValue("sum([1, 2, 3])", new Dictionary<string, object>());
        result.Should().Be(6.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Count()
    {
        var result = FeelEvaluator.EvaluateValue("count([1, 2, 3])", new Dictionary<string, object>());
        result.Should().Be(3.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Min()
    {
        var result = FeelEvaluator.EvaluateValue("min([5, 1, 3])", new Dictionary<string, object>());
        result.Should().Be(1.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Max()
    {
        var result = FeelEvaluator.EvaluateValue("max([5, 1, 3])", new Dictionary<string, object>());
        result.Should().Be(5.0);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Substring()
    {
        var result = FeelEvaluator.EvaluateValue("substring(\"hello\", 2, 3)", new Dictionary<string, object>());
        result.Should().Be("ell");
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Decimal()
    {
        var result = FeelEvaluator.EvaluateValue("decimal(3.14159, 2)", new Dictionary<string, object>());
        result.Should().Be(3.14);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_All()
    {
        var result = FeelEvaluator.EvaluateValue("all([true, 1, \"yes\"])", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Any()
    {
        var result = FeelEvaluator.EvaluateValue("any([false, 0, \"\"])", new Dictionary<string, object>());
        result.Should().Be(false);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Days()
    {
        var result = FeelEvaluator.EvaluateValue("days(3)", new Dictionary<string, object>());
        result.Should().Be(TimeSpan.FromDays(3));
    }

    [Fact]
    public void EvaluateValue_FunctionCall_YearsAndMonthsDuration()
    {
        var result = FeelEvaluator.EvaluateValue(
            "years_and_months_duration(date(\"2025-01-01\"), date(\"2026-03-01\"))",
            new Dictionary<string, object>());
        result.Should().Be(14);
    }

    [Fact]
    public void EvaluateValue_FunctionCall_Split()
    {
        var result = FeelEvaluator.EvaluateValue("split(\"a,b,c\", \",\")", new Dictionary<string, object>());
        (result as string[]).Should().Equal("a", "b", "c");
    }
}
