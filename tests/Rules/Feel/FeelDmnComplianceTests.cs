namespace Muonroi.Rules.Tests.Feel;

public class FeelDmnComplianceTests
{
    [Theory]
    [InlineData("list_contains([1,2,3], 2)", true)]
    [InlineData("count([1,2,3,4,5])", 5d)]
    [InlineData("min([5,2,8,1,9])", 1d)]
    [InlineData("max([5,2,8,1,9])", 9d)]
    [InlineData("sum([1,2,3,4,5])", 15d)]
    [InlineData("mean([1,2,3,4,5])", 3d)]
    public void BuiltInFunction_ListFunctions_ReturnsExpectedResult(string expression, object expected)
    {
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("string_length('hello')", 5d)]
    [InlineData("substring('hello', 2, 3)", "ell")]
    [InlineData("substring_before('hello world', ' ')", "hello")]
    [InlineData("substring_after('hello world', ' ')", "world")]
    public void BuiltInFunction_StringFunctions_ReturnsExpectedResult(string expression, object expected)
    {
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuiltInFunction_Split_ReturnsExpectedSequence()
    {
        object? result = FeelEvaluator.EvaluateValue("split('a,b,c', ',')", new Dictionary<string, object>());
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void Expression_ForLoop_ReturnsExpectedSequence()
    {
        object? result = FeelEvaluator.EvaluateValue("for i in 1..5 return i * 2", new Dictionary<string, object>());
        Assert.Equal(new object?[] { 2d, 4d, 6d, 8d, 10d }, result);
    }

    [Theory]
    [InlineData("some x in [1,2,3,4,5] satisfies x > 3", true)]
    [InlineData("every x in [1,2,3,4,5] satisfies x > 0", true)]
    [InlineData("every x in [1,2,3,4,5] satisfies x > 3", false)]
    public void Expression_Quantified_ReturnsExpectedBoolean(string expression, bool expected)
    {
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("if 10 > 5 then 'yes' else 'no'", "yes")]
    [InlineData("if 10 < 5 then 'yes' else 'no'", "no")]
    public void Expression_IfThenElse_ReturnsCorrectBranch(string expression, string expected)
    {
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Expression_Context_CanAccessNestedProperties()
    {
        string expression = "{x: 10, y: 20, sum: x + y}.sum";
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(30d, result);
    }

    [Fact]
    public void Expression_FilterExpression_FiltersCorrectly()
    {
        object? result = FeelEvaluator.EvaluateValue("[1,2,3,4,5][item > 2]", new Dictionary<string, object>());
        Assert.Equal(new object?[] { 3d, 4d, 5d }, result);
    }

    [Theory]
    [InlineData("10 between 5 and 15", true)]
    [InlineData("10 between 15 and 20", false)]
    [InlineData("10 instance of number", true)]
    [InlineData("'abc' instance of number", false)]
    public void Expression_RangesAndTypes_WorkCorrectly(string expression, bool expected)
    {
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5 in [1..10]", true)]
    [InlineData("1 in (1..10]", false)]
    [InlineData("10 in [1..10)", false)]
    [InlineData("5 in (1..10)", true)]
    [InlineData("1 in (1..10)", false)]
    public void Expression_OpenClosedRanges_WorkCorrectly(string expression, bool expected)
    {
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Expression_FunctionDefinition_WorksInContext()
    {
        string expression = "{add: function(x, y) x + y, result: add(2, 3)}.result";
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(5d, result);
    }

    [Fact]
    public void Expression_FunctionDefinition_CapturesContext()
    {
        string expression = "{base: 10, addBase: function(x) x + base, result: addBase(5)}.result";
        object? result = FeelEvaluator.EvaluateValue(expression, new Dictionary<string, object>());
        Assert.Equal(15d, result);
    }
}
