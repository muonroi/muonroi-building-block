namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ProliferationModelsTests
{
    [Fact]
    public void NeuronScenario_Serialization_RoundTrips()
    {
        NeuronScenario scenario = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            SeedRuleCode = "ORDER_VALIDATE",
            ScenarioName = "Null order amount",
            Type = ScenarioType.Business,
            Scope = ProliferationScope.Rule,
            GenerationDepth = 0,
            ProliferationReason = "Tests null boundary on amount field",
            InputFacts = JsonSerializer.SerializeToElement(new { amount = (int?)null, currency = "USD" }),
            ExpectedBehavior = "should fail with validation error",
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            TenantId = "tenant-1"
        };

        string json = JsonSerializer.Serialize(scenario);
        NeuronScenario? deserialized = JsonSerializer.Deserialize<NeuronScenario>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(scenario.Id);
        deserialized.SeedRuleCode.Should().Be("ORDER_VALIDATE");
        deserialized.Type.Should().Be(ScenarioType.Business);
        deserialized.Status.Should().Be(ScenarioStatus.Pending);
    }

    [Fact]
    public void ScenarioResult_Serialization_RoundTrips()
    {
        ScenarioResult result = new()
        {
            ScenarioId = Guid.NewGuid().ToString("N"),
            IsSuccess = true,
            MatchesExpectation = true,
            ActualBehavior = "passed",
            OutputFacts = JsonSerializer.SerializeToElement(new { total = 100 }),
            Errors = [],
            Duration = TimeSpan.FromMilliseconds(150),
            ExecutedAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(result);
        ScenarioResult? deserialized = JsonSerializer.Deserialize<ScenarioResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
        deserialized.MatchesExpectation.Should().BeTrue();
    }

    [Fact]
    public void NeuronScenario_TreeStructure_ParentChild()
    {
        string parentId = Guid.NewGuid().ToString("N");
        string childId = Guid.NewGuid().ToString("N");

        NeuronScenario parent = new()
        {
            Id = parentId,
            SeedRuleCode = "ORDER_VALIDATE",
            ScenarioName = "Base scenario",
            GenerationDepth = 0,
            ProliferationReason = "Initial seed",
            Status = ScenarioStatus.Passed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        NeuronScenario child = new()
        {
            Id = childId,
            SeedRuleCode = "ORDER_VALIDATE",
            ScenarioName = "Child scenario",
            ParentScenarioId = parentId,
            GenerationDepth = 1,
            ProliferationReason = "Derived from parent",
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        child.ParentScenarioId.Should().Be(parent.Id);
        child.GenerationDepth.Should().Be(parent.GenerationDepth + 1);
        child.SeedRuleCode.Should().Be(parent.SeedRuleCode);
    }

    [Fact]
    public void ProliferationPlan_EmptyScenarios()
    {
        ProliferationPlan plan = new()
        {
            SeedRuleCode = "TEST",
            AiModelUsed = "qwen2.5-coder:14b",
            Scenarios = [],
            GenerationDuration = TimeSpan.FromSeconds(5)
        };

        plan.Scenarios.Should().BeEmpty();
        plan.Scope.Should().Be(ProliferationScope.Rule);
    }

    [Fact]
    public void ProliferationStats_DefaultValues()
    {
        ProliferationStats stats = new();

        stats.TotalScenarios.Should().Be(0);
        stats.Passed.Should().Be(0);
        stats.Failed.Should().Be(0);
        stats.Pending.Should().Be(0);
        stats.MaxDepthReached.Should().Be(0);
        stats.BySeedRule.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ScenarioStatus.Pending, 0)]
    [InlineData(ScenarioStatus.Running, 1)]
    [InlineData(ScenarioStatus.Passed, 2)]
    [InlineData(ScenarioStatus.Failed, 3)]
    [InlineData(ScenarioStatus.Error, 4)]
    [InlineData(ScenarioStatus.Skipped, 5)]
    public void ScenarioStatus_HasExpectedValues(ScenarioStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }
}
