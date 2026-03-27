using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Proliferation.Execution;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ScenarioExecutorTests
{
    [Theory]
    [InlineData("should pass", true, true)]
    [InlineData("should pass with order total", true, true)]
    [InlineData("should succeed", true, true)]
    [InlineData("should fail", true, false)]
    [InlineData("should fail with validation error", true, false)]
    [InlineData("should pass", false, false)]
    [InlineData("should fail", false, true)]
    [InlineData(null, true, true)]
    [InlineData(null, false, true)]
    [InlineData("", true, true)]
    public void EvaluateExpectation_MatchesCorrectly(string? expected, bool isSuccess, bool shouldMatch)
    {
        OrchestratorResult result = isSuccess
            ? OrchestratorResult.Success(ExecutionMode.BestEffort, new FactBag(), new Dictionary<string, RuleResult>())
            : OrchestratorResult.Failure(ExecutionMode.BestEffort, new FactBag(), new Dictionary<string, RuleResult>(), ["error"], []);

        bool matches = ScenarioExecutor.EvaluateExpectation(expected, result);
        matches.Should().Be(shouldMatch);
    }
}
