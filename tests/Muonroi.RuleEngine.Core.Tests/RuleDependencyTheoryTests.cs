namespace Muonroi.RuleEngine.Core.Tests;

public class RuleDependencyTheoryTests
{
    private class TestRule : IRule<string>
    {
        public string Name { get; init; } = "TestRule";
        public string Code { get; init; } = "TestRule";
        public int Order { get; init; } = 0;
        public IReadOnlyList<string> DependsOn { get; init; } = [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task<RuleResult> EvaluateAsync(string context, FactBag facts, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Rule_WithEmptyDependencies_HasCorrectCount(int dependencyCount)
    {
        string[] dependencies = new string[dependencyCount];
        for (int i = 0; i < dependencyCount; i++)
        {
            dependencies[i] = $"Dependency{i}";
        }

        TestRule rule = new() { DependsOn = dependencies };

        Assert.Equal(dependencyCount, rule.DependsOn.Count);
    }

    [Theory]
    [InlineData("Dep1")]
    [InlineData("Dep2")]
    [InlineData("DependencyA")]
    [InlineData("DependencyB")]
    public void Rule_WithSingleDependency_ContainsDependency(string dependency)
    {
        TestRule rule = new() { DependsOn = new[] { dependency } };

        Assert.Contains(dependency, rule.DependsOn);
        Assert.Single(rule.DependsOn);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Rule_WithEmptyStringDependency_StillStoresIt(string emptyDependency)
    {
        TestRule rule = new() { DependsOn = new[] { emptyDependency } };

        Assert.Contains(emptyDependency, rule.DependsOn);
    }

    [Theory]
    [InlineData("RuleA", "RuleB")]
    [InlineData("RuleX", "RuleY")]
    [InlineData("Dependency1", "Dependency2")]
    public void Rule_WithMultipleDependencies_ContainsAllDependencies(string dep1, string dep2)
    {
        TestRule rule = new() { DependsOn = new[] { dep1, dep2 } };

        Assert.Contains(dep1, rule.DependsOn);
        Assert.Contains(dep2, rule.DependsOn);
        Assert.Equal(2, rule.DependsOn.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public void Rule_WithDifferentOrders_HasCorrectOrder(int order)
    {
        TestRule rule = new() { Order = order };

        Assert.Equal(order, rule.Order);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(-100)]
    public void Rule_WithNegativeOrder_HasCorrectOrder(int negativeOrder)
    {
        TestRule rule = new() { Order = negativeOrder };

        Assert.Equal(negativeOrder, rule.Order);
    }

    [Theory]
    [InlineData("RuleCode1")]
    [InlineData("RuleCode2")]
    [InlineData("UPPERCASE")]
    [InlineData("lowercase")]
    [InlineData("MixedCase")]
    public void Rule_WithDifferentCodes_HasCorrectCode(string code)
    {
        TestRule rule = new() { Code = code };

        Assert.Equal(code, rule.Code);
    }

    [Theory]
    [InlineData("Rule Name 1")]
    [InlineData("Rule Name 2")]
    [InlineData("Special@Rule")]
    [InlineData("Rule#123")]
    public void Rule_WithDifferentNames_HasCorrectName(string name)
    {
        TestRule rule = new() { Name = name };

        Assert.Equal(name, rule.Name);
    }
}
