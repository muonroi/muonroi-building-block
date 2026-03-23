namespace Muonroi.Rules.Tests.Feel;

public class FeelEvaluatorAdvancedExprTests
{
    private sealed class SampleOrder
    {
        public double Total { get; set; }
        public SampleBox Box { get; set; } = new();
    }

    private sealed class SampleBox
    {
        public double Value = 7.0;
    }

    // For expressions

    [Fact]
    public void EvaluateValue_ForLoop_DescendingRange()
    {
        var result = FeelEvaluator.EvaluateValue("for i in 5..1 return i", new Dictionary<string, object>());
        var items = (result as object?[])!;
        items.Should().HaveCount(5);
        items[0].Should().Be(5.0);
        items[4].Should().Be(1.0);
    }

    [Fact]
    public void EvaluateValue_ForLoop_WithBodyExpression()
    {
        var result = FeelEvaluator.EvaluateValue("for x in 1..3 return x * 2", new Dictionary<string, object>());
        var items = (result as object?[])!;
        items.Should().ContainInOrder(2.0, 4.0, 6.0);
    }

    [Fact]
    public void EvaluateValue_ForLoop_OverVariable()
    {
        var vars = new Dictionary<string, object>
        {
            ["items"] = new object[] { 10.0, 20.0, 30.0 }
        };
        var result = FeelEvaluator.EvaluateValue("for x in items return x + 1", vars);
        var items = (result as object?[])!;
        items.Should().ContainInOrder(11.0, 21.0, 31.0);
    }

    // Quantified expressions

    [Fact]
    public void EvaluateValue_Some_ShouldReturnTrue_WhenAnyMatch()
    {
        var result = FeelEvaluator.EvaluateValue(
            "some x in [1, 2, 3] satisfies x > 2",
            new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_Some_ShouldReturnFalse_WhenNoneMatch()
    {
        var result = FeelEvaluator.EvaluateValue(
            "some x in [1, 2, 3] satisfies x > 5",
            new Dictionary<string, object>());
        result.Should().Be(false);
    }

    [Fact]
    public void EvaluateValue_Every_ShouldReturnTrue_WhenAllMatch()
    {
        var result = FeelEvaluator.EvaluateValue(
            "every x in [1, 2, 3] satisfies x > 0",
            new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_Every_ShouldReturnFalse_WhenNotAllMatch()
    {
        var result = FeelEvaluator.EvaluateValue(
            "every x in [1, 2, 3] satisfies x > 1",
            new Dictionary<string, object>());
        result.Should().Be(false);
    }

    // Filter expressions

    [Fact]
    public void EvaluateValue_Filter_ShouldFilterList()
    {
        var vars = new Dictionary<string, object>
        {
            ["items"] = new object[] { 1.0, 2.0, 3.0, 4.0, 5.0 }
        };
        var result = FeelEvaluator.EvaluateValue("items[item > 3]", vars);
        var filtered = result as object?[];
        filtered.Should().NotBeNull();
        filtered!.Should().HaveCount(2);
    }

    // Between expressions

    [Fact]
    public void EvaluateValue_Between_InRange_ShouldReturnTrue()
    {
        var vars = new Dictionary<string, object> { ["x"] = 5.0 };
        var result = FeelEvaluator.EvaluateValue("x between 1 and 10", vars);
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_Between_OutOfRange_ShouldReturnFalse()
    {
        var vars = new Dictionary<string, object> { ["x"] = 15.0 };
        var result = FeelEvaluator.EvaluateValue("x between 1 and 10", vars);
        result.Should().Be(false);
    }

    // Instance-of expressions

    [Fact]
    public void EvaluateValue_InstanceOf_String_ShouldReturnTrue()
    {
        var result = FeelEvaluator.EvaluateValue("\"hello\" instance of string", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_InstanceOf_Number_ShouldReturnTrue()
    {
        var result = FeelEvaluator.EvaluateValue("42 instance of number", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_InstanceOf_Boolean_ShouldReturnTrue()
    {
        var result = FeelEvaluator.EvaluateValue("true instance of boolean", new Dictionary<string, object>());
        result.Should().Be(true);
    }

    // Complex expressions

    [Fact]
    public void Evaluate_ComplexNested_IfThenWithComparison()
    {
        var vars = new Dictionary<string, object> { ["score"] = 85.0 };
        var result = FeelEvaluator.EvaluateValue(
            "if score >= 90 then \"A\" else if score >= 80 then \"B\" else \"C\"", vars);
        result.Should().Be("B");
    }

    [Fact]
    public void EvaluateValue_StringEqualityComparison()
    {
        var vars = new Dictionary<string, object> { ["status"] = "active" };
        var result = FeelEvaluator.Evaluate("status = \"active\"", vars);
        result.Should().BeTrue();
    }

    // Parser behavior: null literal returns string "null"
    [Fact]
    public void EvaluateValue_NullLiteral_ReturnsStringNull()
    {
        // The parser treats "null" as an identifier/keyword — returns string "null"
        var result = FeelEvaluator.EvaluateValue("null", new Dictionary<string, object>());
        result.Should().Be("null");
    }

    // Parser behavior: list index is 0-based in the parser
    [Fact]
    public void EvaluateValue_ListIndex_ZeroBased()
    {
        var vars = new Dictionary<string, object>
        {
            ["items"] = new object[] { "a", "b", "c" }
        };
        // Parser uses 0-based indexing: items[2] returns "c"
        var result = FeelEvaluator.EvaluateValue("items[2]", vars);
        result.Should().Be("c");
    }

    // Parser behavior: undefined variable returns variable name
    [Fact]
    public void EvaluateValue_UndefinedVariable_ReturnsName()
    {
        var result = FeelEvaluator.EvaluateValue("undefined_var", new Dictionary<string, object>());
        result.Should().Be("undefined_var");
    }

    // Nested function calls
    [Fact]
    public void EvaluateValue_NestedFunctionCalls()
    {
        var result = FeelEvaluator.EvaluateValue("abs(floor(-3.7))", new Dictionary<string, object>());
        result.Should().Be(4.0);
    }

    [Fact]
    public void EvaluateValue_EmptyList()
    {
        var result = FeelEvaluator.EvaluateValue("[]", new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    // Context literal
    [Fact]
    public void EvaluateValue_ContextLiteral()
    {
        var result = FeelEvaluator.EvaluateValue("{a: 1, b: 2}", new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    // Multiple operators
    [Fact]
    public void EvaluateValue_MultipleOperators_Precedence()
    {
        var result = FeelEvaluator.EvaluateValue("2 + 3 * 4", new Dictionary<string, object>());
        result.Should().Be(14.0);
    }

    [Fact]
    public void EvaluateValue_ParenthesizedExpressions()
    {
        var result = FeelEvaluator.EvaluateValue("(2 + 3) * 4", new Dictionary<string, object>());
        result.Should().Be(20.0);
    }

    [Fact]
    public void EvaluateValue_UnaryMinus()
    {
        var result = FeelEvaluator.EvaluateValue("-10", new Dictionary<string, object>());
        result.Should().Be(-10.0);
    }

    [Fact]
    public void EvaluateValue_ChainedComparisons()
    {
        var vars = new Dictionary<string, object> { ["a"] = 5.0, ["b"] = 3.0 };
        var result = FeelEvaluator.Evaluate("a > b and b > 0", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_OrOperator_SecondOperandTrue()
    {
        var vars = new Dictionary<string, object> { ["x"] = 1.0 };
        var result = FeelEvaluator.Evaluate("x > 10 or x > 0", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_PropertyAccess_OnDictVariable()
    {
        var vars = new Dictionary<string, object>
        {
            ["obj"] = new Dictionary<string, object> { ["key"] = "value" }
        };
        var result = FeelEvaluator.EvaluateValue("obj.key", vars);
        result.Should().Be("value");
    }

    [Fact]
    public void EvaluateValue_StringComparison_NotEqual()
    {
        var vars = new Dictionary<string, object> { ["s"] = "abc" };
        var result = FeelEvaluator.Evaluate("s != \"xyz\"", vars);
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateValue_PreprocessesFunctionAliases()
    {
        FeelEvaluator.EvaluateValue("string length(\"hello\")", new Dictionary<string, object>())
            .Should().Be(5.0);
        FeelEvaluator.EvaluateValue("upper case(\"hello\")", new Dictionary<string, object>())
            .Should().Be("HELLO");
    }

    [Fact]
    public void EvaluateValue_PropertyAccess_OnObjectPropertyAndField()
    {
        var vars = new Dictionary<string, object>
        {
            ["order"] = new SampleOrder { Total = 42.0 }
        };

        FeelEvaluator.EvaluateValue("order.total", vars).Should().Be(42.0);
        FeelEvaluator.EvaluateValue("order.box.value", vars).Should().Be(7.0);
    }

    [Fact]
    public void EvaluateValue_IndexedPropertyAccess_ShouldResolveNestedMember()
    {
        var vars = new Dictionary<string, object>
        {
            ["orders"] = new object[]
            {
                new Dictionary<string, object> { ["id"] = "A1" },
                new Dictionary<string, object> { ["id"] = "B2" }
            }
        };

        FeelEvaluator.EvaluateValue("orders[1].id", vars).Should().Be("B2");
    }

    [Fact]
    public void EvaluateValue_InstanceOf_ContextDateAndTime_ShouldReturnTrue()
    {
        FeelEvaluator.EvaluateValue("{a: 1} instance of context", new Dictionary<string, object>())
            .Should().Be(true);
        FeelEvaluator.EvaluateValue("date(\"2026-03-21\") instance of date", new Dictionary<string, object>())
            .Should().Be(true);
        FeelEvaluator.EvaluateValue("time(\"14:30:00\") instance of time", new Dictionary<string, object>())
            .Should().Be(true);
    }

    [Fact]
    public void EvaluateValue_InOperator_WithBracketListAndEmptyParenList_ShouldReturnExpectedResults()
    {
        FeelEvaluator.EvaluateValue("2 in [1, 2, 3]", new Dictionary<string, object>())
            .Should().Be(true);
        FeelEvaluator.EvaluateValue("2 in ()", new Dictionary<string, object>())
            .Should().Be(false);
    }

    [Fact]
    public void EvaluateValue_ListLiteralRangeAndEmptyContext_ShouldBeParsed()
    {
        FeelEvaluator.EvaluateValue("[3..1]", new Dictionary<string, object>())
            .Should().BeEquivalentTo(new object?[] { 3.0, 2.0, 1.0 });
        FeelEvaluator.EvaluateValue("{}", new Dictionary<string, object>())
            .Should().BeAssignableTo<IDictionary<string, object?>>();
    }

    [Fact]
    public void EvaluateValue_RoundFunction_ShouldUseParserFallback()
    {
        FeelEvaluator.EvaluateValue("round(3.14159, 2)", new Dictionary<string, object>())
            .Should().Be(3.14);
    }

    [Fact]
    public void EvaluateValue_UnsupportedFunctionAndMalformedRange_ShouldReturnNull()
    {
        FeelEvaluator.EvaluateValue("missing_fn(1)", new Dictionary<string, object>())
            .Should().BeNull();
        FeelEvaluator.EvaluateValue("1 in [1..10", new Dictionary<string, object>())
            .Should().BeNull();
    }

    [Fact]
    public void EvaluateValue_NestedForExpression_UsesParserForPath()
    {
        object? result = FeelEvaluator.EvaluateValue(
            "{details: [1, 2, 3], result: for d in details return d + 1}.result",
            new Dictionary<string, object>());

        result.Should().BeEquivalentTo(new object?[] { 2.0, 3.0, 4.0 });
    }

    [Fact]
    public void EvaluateValue_NestedQuantifiedExpression_UsesParserQuantifierPath()
    {
        FeelEvaluator.EvaluateValue(
                "{details: [1, 2, 3], result: every d in details satisfies d > 0}.result",
                new Dictionary<string, object>())
            .Should().Be(true);

        FeelEvaluator.EvaluateValue(
                "{details: [1, 2, 3], result: some d in details satisfies d > 2}.result",
                new Dictionary<string, object>())
            .Should().Be(true);
    }
}
