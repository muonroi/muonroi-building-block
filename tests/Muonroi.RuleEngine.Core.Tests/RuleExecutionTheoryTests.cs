using Muonroi.RuleEngine.Core;
using Muonroi.RuleEngine.Core.Runtime;
using Xunit;

namespace Muonroi.RuleEngine.Core.Tests;

public class RuleExecutionTheoryTests
{
    private class ConfigurableTestRule : IRule<string>
    {
        public string Name { get; init; } = "TestRule";
        public string Code { get; init; } = "TestRule";
        public int Order { get; init; } = 0;
        public IReadOnlyList<string> DependsOn { get; init; } = [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint { get; init; } = HookPoint.BeforePersist;
        public RuleType Type { get; init; } = RuleType.Validation;
        public Func<string, FactBag, CancellationToken, Task<RuleResult>>? EvaluateFunc { get; init; }

        public Task<RuleResult> EvaluateAsync(string context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            return EvaluateFunc?.Invoke(context, facts, cancellationToken) ?? Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(HookPoint.BeforePersist)]
    [InlineData(HookPoint.AfterPersist)]
    [InlineData(HookPoint.OnFailure)]
    [InlineData(HookPoint.OnSuccess)]
    public async Task Rule_WithDifferentHookPoints_ExecutesCorrectly(HookPoint hookPoint)
    {
        ConfigurableTestRule rule = new() { HookPoint = hookPoint };
        FactBag bag = new();

        RuleResult result = await rule.EvaluateAsync("context", bag);

        Assert.True(result.IsSuccess);
        Assert.Equal(hookPoint, rule.HookPoint);
    }

    [Theory]
    [InlineData(RuleType.Validation)]
    [InlineData(RuleType.Business)]
    [InlineData(RuleType.EmptyTypes)]
    public async Task Rule_WithDifferentTypes_ExecutesCorrectly(RuleType ruleType)
    {
        ConfigurableTestRule rule = new() { Type = ruleType };
        FactBag bag = new();

        RuleResult result = await rule.EvaluateAsync("context", bag);

        Assert.True(result.IsSuccess);
        Assert.Equal(ruleType, rule.Type);
    }

    [Theory]
    [InlineData("context1")]
    [InlineData("context2")]
    [InlineData("test-context")]
    [InlineData("")]
    public async Task Rule_WithDifferentContexts_ExecutesCorrectly(string context)
    {
        string receivedContext = string.Empty;
        ConfigurableTestRule rule = new()
        {
            EvaluateFunc = (ctx, facts, ct) =>
            {
                receivedContext = ctx;
                return Task.FromResult(RuleResult.Passed());
            }
        };
        FactBag bag = new();

        await rule.EvaluateAsync(context, bag);

        Assert.Equal(context, receivedContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RuleResult_WithDifferentSuccessStates_ReturnsCorrectly(bool isSuccess)
    {
        ConfigurableTestRule rule = new()
        {
            EvaluateFunc = (ctx, facts, ct) => Task.FromResult(
                isSuccess ? RuleResult.Passed() : RuleResult.Failure("Validation failed"))
        };
        FactBag bag = new();

        RuleResult result = await rule.EvaluateAsync("context", bag);

        Assert.Equal(isSuccess, result.IsSuccess);
    }

    [Theory]
    [InlineData("Error message 1")]
    [InlineData("Validation failed")]
    [InlineData("Business rule violation")]
    [InlineData("")]
    public async Task RuleResult_WithDifferentErrorMessages_StoresCorrectly(string errorMessage)
    {
        ConfigurableTestRule rule = new()
        {
            EvaluateFunc = (ctx, facts, ct) => Task.FromResult(RuleResult.Failure(errorMessage))
        };
        FactBag bag = new();

        RuleResult result = await rule.EvaluateAsync("context", bag);

        Assert.False(result.IsSuccess);
        Assert.Contains(errorMessage, result.Errors);
    }

    [Fact]
    public Task RuleResult_Passed_HasNoErrors()
    {
        RuleResult result = RuleResult.Passed();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(-5)]
    public Task Activation_WithDifferentPriorities_HasCorrectPriority(int priority)
    {
        Activation activation = new(async _ => await Task.CompletedTask, priority);

        Assert.Equal(priority, activation.Priority);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("GroupA")]
    [InlineData("GroupB")]
    [InlineData("")]
    public Task Activation_WithDifferentGroups_HasCorrectGroup(string? group)
    {
        Activation activation = new(async _ => await Task.CompletedTask, 10, group);

        Assert.Equal(group, activation.Group);
        return Task.CompletedTask;
    }
}