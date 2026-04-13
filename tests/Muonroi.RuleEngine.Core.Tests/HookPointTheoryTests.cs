using Muonroi.RuleEngine.Core;
using Xunit;

namespace Muonroi.RuleEngine.Core.Tests;

public class HookPointTheoryTests
{
    private class TestRuleWithHook : IRule<string>
    {
        public string Name => "TestRule";
        public string Code => "TestRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint { get; init; }
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
    [InlineData(HookPoint.BeforePersist)]
    [InlineData(HookPoint.AfterPersist)]
    [InlineData(HookPoint.OnSuccess)]
    [InlineData(HookPoint.OnFailure)]
    public void Rule_WithDifferentHookPoints_SetsCorrectly(HookPoint hookPoint)
    {
        TestRuleWithHook rule = new() { HookPoint = hookPoint };

        Assert.Equal(hookPoint, rule.HookPoint);
    }

    [Theory]
    [InlineData(HookPoint.BeforePersist, HookPoint.BeforePersist)]
    [InlineData(HookPoint.AfterPersist, HookPoint.AfterPersist)]
    [InlineData(HookPoint.OnSuccess, HookPoint.OnSuccess)]
    [InlineData(HookPoint.OnFailure, HookPoint.OnFailure)]
    public void Rule_SameHookPoints_AreEqual(HookPoint hook1, HookPoint hook2)
    {
        Assert.Equal(hook1, hook2);
    }

    [Theory]
    [InlineData(HookPoint.BeforePersist, HookPoint.AfterPersist)]
    [InlineData(HookPoint.OnSuccess, HookPoint.OnFailure)]
    [InlineData(HookPoint.BeforePersist, HookPoint.OnSuccess)]
    public void Rule_DifferentHookPoints_AreNotEqual(HookPoint hook1, HookPoint hook2)
    {
        Assert.NotEqual(hook1, hook2);
    }
}
