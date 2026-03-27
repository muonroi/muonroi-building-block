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
}
