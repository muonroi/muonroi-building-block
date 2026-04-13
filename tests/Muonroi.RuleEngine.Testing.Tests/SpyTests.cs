namespace Muonroi.RuleEngine.Testing.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Muonroi.RuleEngine.Testing;
using Muonroi.RuleEngine.Abstractions;
using Xunit;
using Moq;

public class SpyTests
{
    public class TestRule : IRule<string>
    {
        public string Code => "RULE001";
        public int Order => 1;
        public async Task<RuleResult> ExecuteAsync(string context, FactBag facts, CancellationToken ct = default)
        {
            facts.Set("test", "value");
            return await Task.FromResult(RuleResult.Success());
        }
    }

    [Fact]
    public async Task MRuleOrchestratorSpy_ShouldCaptureExecution()
    {
        // Arrange
        var rules = new List<IRule<string>> { new TestRule() };
        var spy = new MRuleOrchestratorSpy<string>(rules);

        // Act
        await spy.ExecuteAsync("context");

        // Assert
        Assert.NotEmpty(spy.ExecutionRecords);
        Assert.Equal("RULE001", spy.ExecutionRecords[0].RuleCode);
        Assert.True(spy.ExecutionRecords[0].IsSuccess);
    }
}
