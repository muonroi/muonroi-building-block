namespace Muonroi.Rules.Tests.Feel;

public class FeelEvaluatorAdvancedTests
{
    [Fact]
    public void Evaluate_ShouldSupportLogicalOperators()
    {
        Dictionary<string, object> vars = new()
        {
            ["age"] = 25,
            ["status"] = "ACTIVE",
            ["isBlocked"] = false
        };

        Assert.True(FeelEvaluator.Evaluate("age >= 18 AND status in (ACTIVE,PENDING)", vars));
        Assert.True(FeelEvaluator.Evaluate("NOT (isBlocked = true)", vars));
        Assert.False(FeelEvaluator.Evaluate("age < 18 OR status = SUSPENDED", vars));
    }

    [Fact]
    public void Evaluate_ShouldSupportArithmeticExpressions()
    {
        Dictionary<string, object> vars = new()
        {
            ["price"] = 20,
            ["quantity"] = 6
        };

        Assert.True(FeelEvaluator.Evaluate("price * quantity > 100", vars));
        Assert.True(FeelEvaluator.Evaluate("round(price * quantity, 0) = 120", vars));
    }

    [Fact]
    public void Evaluate_ShouldSupportDateFunctions()
    {
        Dictionary<string, object> vars = new()
        {
            ["createdAt"] = DateTime.UtcNow.AddDays(-5)
        };

        Assert.True(FeelEvaluator.Evaluate("createdAt >= today() - days(30)", vars));
        Assert.False(FeelEvaluator.Evaluate("createdAt < today() - days(30)", vars));
    }

    [Fact]
    public void Evaluate_ShouldSupportStringFunctionsAndOperators()
    {
        Dictionary<string, object> vars = new()
        {
            ["email"] = "dev@company.com",
            ["name"] = "Mr. John",
            ["status"] = "active"
        };

        Assert.True(FeelEvaluator.Evaluate("email contains '@company.com'", vars));
        Assert.True(FeelEvaluator.Evaluate("name startsWith 'Mr.'", vars));
        Assert.True(FeelEvaluator.Evaluate("upper(status) = 'ACTIVE'", vars));
    }

    [Fact]
    public void Evaluate_ShouldSupportNestedPathsAndWildcards()
    {
        Dictionary<string, object> vars = new()
        {
            ["order"] = new
            {
                customer = new { vipStatus = true }
            },
            ["items"] = new[]
        {
            new { price = 100.0 },
            new { price = 250.0 },
            new { price = 300.0 }
        }
        };

        Assert.True(FeelEvaluator.Evaluate("order.customer.vipStatus = true", vars));
        Assert.True(FeelEvaluator.Evaluate("items[0].price = 100", vars));
        Assert.True(FeelEvaluator.Evaluate("sum(items[*].price) > 500", vars));
    }

    [Fact]
    public void EvaluateValue_ForExpressionInsideSum_WithNestedClrProperties_ShouldReturnCorrectSum()
    {
        // Simulates the real scenario: sum(for d in details return d.vgm.wgt)
        // where details is a List<DetailV4> with nested CLR objects
        var details = new[]
        {
            new { Vgm = new { Wgt = 40.0 } },
            new { Vgm = new { Wgt = 40.0 } },
            new { Vgm = new { Wgt = 40.0 } }
        };

        Dictionary<string, object> vars = new()
        {
            ["details"] = details
        };

        object? result = FeelEvaluator.EvaluateValue("sum(for d in details return d.vgm.wgt)", vars);
        Assert.NotNull(result);
        Assert.Equal(120.0, Convert.ToDouble(result));
    }

    [Fact]
    public void EvaluateValue_ForExpressionInsideSum_WithDictionaryItems_ShouldReturnCorrectSum()
    {
        var details = new List<object>
        {
            new Dictionary<string, object> { ["vgm"] = new Dictionary<string, object> { ["wgt"] = 50.0 } },
            new Dictionary<string, object> { ["vgm"] = new Dictionary<string, object> { ["wgt"] = 70.0 } }
        };

        Dictionary<string, object> vars = new()
        {
            ["details"] = details
        };

        object? result = FeelEvaluator.EvaluateValue("sum(for d in details return d.vgm.wgt)", vars);
        Assert.NotNull(result);
        Assert.Equal(120.0, Convert.ToDouble(result));
    }

    [Fact]
    public void EvaluateValue_ForExpressionInsideSum_ComparedToThreshold_ShouldEvaluateCorrectly()
    {
        // Full end-to-end: sum(for d in details return d.vgm.wgt) > 100
        var details = new[]
        {
            new { Vgm = new { Wgt = 40.0 } },
            new { Vgm = new { Wgt = 40.0 } },
            new { Vgm = new { Wgt = 40.0 } }
        };

        Dictionary<string, object> vars = new()
        {
            ["details"] = details
        };

        // sum = 120, so 120 <= 100 should be false
        Assert.False(FeelEvaluator.Evaluate("sum(for d in details return d.vgm.wgt) <= 100", vars));
        // 120 > 100 should be true
        Assert.True(FeelEvaluator.Evaluate("sum(for d in details return d.vgm.wgt) > 100", vars));
    }

    [Fact]
    public void EvaluateValue_ForExpressionWithRange_ShouldWork()
    {
        Dictionary<string, object> vars = new();
        object? result = FeelEvaluator.EvaluateValue("sum(for x in 1..5 return x)", vars);
        Assert.NotNull(result);
        Assert.Equal(15.0, Convert.ToDouble(result));
    }

    [Fact]
    public void EvaluateValue_QuantifiedExpressionInsideParser_SomeAndEvery()
    {
        var items = new[] { 10.0, 20.0, 30.0 };
        Dictionary<string, object> vars = new()
        {
            ["values"] = items
        };

        // "some x in values satisfies x > 25" should be true (30 > 25)
        Assert.True(FeelEvaluator.Evaluate("some x in values satisfies x > 25", vars));
        // "every x in values satisfies x > 5" should be true
        Assert.True(FeelEvaluator.Evaluate("every x in values satisfies x > 5", vars));
        // "every x in values satisfies x > 15" should be false (10 is not > 15)
        Assert.False(FeelEvaluator.Evaluate("every x in values satisfies x > 15", vars));
    }
}
