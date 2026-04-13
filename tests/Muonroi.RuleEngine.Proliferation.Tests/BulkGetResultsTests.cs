using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.RuleEngine.Proliferation.Store;

namespace Muonroi.RuleEngine.Proliferation.Tests;

/// <summary>
/// Tests for InMemoryProliferationStore.GetResultsByWorkflowAsync.
/// </summary>
public class BulkGetResultsTests
{
    private static NeuronScenario MakeScenario(string id, string seedRuleCode)
    {
        using JsonDocument doc = JsonDocument.Parse("{}");
        return new NeuronScenario
        {
            Id = id,
            SeedRuleCode = seedRuleCode,
            ScenarioName = $"Scenario {id}",
            ProliferationReason = "test",
            InputFacts = doc.RootElement.Clone(),
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ScenarioResult MakeResult(string scenarioId, bool success = true)
        => new()
        {
            ScenarioId = scenarioId,
            IsSuccess = success,
            MatchesExpectation = success,
            Duration = TimeSpan.FromMilliseconds(100),
            ExecutedAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task GetResultsByWorkflowAsync_ReturnsAllResultsForWorkflow()
    {
        // Arrange
        InMemoryProliferationStore store = new();
        NeuronScenario s1 = MakeScenario("s1", "workflow-A");
        NeuronScenario s2 = MakeScenario("s2", "workflow-A");
        await store.SaveScenariosAsync([s1, s2]);
        await store.SaveResultAsync(MakeResult("s1"));
        await store.SaveResultAsync(MakeResult("s2", false));

        // Act
        IReadOnlyList<ScenarioResult> results = await store.GetResultsByWorkflowAsync("workflow-A");

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.ScenarioId).Should().BeEquivalentTo(["s1", "s2"]);
    }

    [Fact]
    public async Task GetResultsByWorkflowAsync_UnknownWorkflow_ReturnsEmpty()
    {
        // Arrange
        InMemoryProliferationStore store = new();
        NeuronScenario s1 = MakeScenario("s1", "workflow-A");
        await store.SaveScenariosAsync([s1]);
        await store.SaveResultAsync(MakeResult("s1"));

        // Act
        IReadOnlyList<ScenarioResult> results = await store.GetResultsByWorkflowAsync("workflow-UNKNOWN");

        // Assert
        results.Should().BeEmpty("unknown workflow has no scenarios or results");
    }

    [Fact]
    public async Task GetResultsByWorkflowAsync_FiltersAcrossWorkflows()
    {
        // Arrange: two workflows with overlapping results
        InMemoryProliferationStore store = new();
        NeuronScenario sA = MakeScenario("sA", "workflow-A");
        NeuronScenario sB = MakeScenario("sB", "workflow-B");
        await store.SaveScenariosAsync([sA, sB]);
        await store.SaveResultAsync(MakeResult("sA", true));
        await store.SaveResultAsync(MakeResult("sB", false));

        // Act
        IReadOnlyList<ScenarioResult> resultsA = await store.GetResultsByWorkflowAsync("workflow-A");
        IReadOnlyList<ScenarioResult> resultsB = await store.GetResultsByWorkflowAsync("workflow-B");

        // Assert: each workflow only sees its own results
        resultsA.Should().HaveCount(1);
        resultsA[0].ScenarioId.Should().Be("sA");

        resultsB.Should().HaveCount(1);
        resultsB[0].ScenarioId.Should().Be("sB");
    }
}
