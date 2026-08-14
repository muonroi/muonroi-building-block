namespace Muonroi.RuleEngine.Proliferation.Tests;

public class DefaultFailureAnalyzerTests
{
    private readonly Mock<IRuleProliferationBrain> _brain = new();
    private readonly ProliferationOptions _options = new()
    {
        EnableFailureFeedback = true,
        MaxScenariosPerFailure = 2,
        MaxScenariosPerRule = 10
    };

    private DefaultFailureAnalyzer CreateAnalyzer() => new(_brain.Object, _options);

    private static NeuronScenario CreateFailedScenario(string id = "failed-1") => new()
    {
        Id = id,
        SeedRuleCode = "TEST",
        ScenarioName = "Failing boundary test",
        ProliferationReason = "test edge case",
        Status = ScenarioStatus.Failed,
        GenerationDepth = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        InputFacts = JsonDocument.Parse("""{"amount": 0}""").RootElement.Clone()
    };

    private static ScenarioResult CreateFailureResult(string scenarioId = "failed-1") => new()
    {
        ScenarioId = scenarioId,
        IsSuccess = false,
        MatchesExpectation = false,
        ActualBehavior = "Threw NullReferenceException",
        Errors = ["NullReferenceException at RuleProcessor.Evaluate"],
        Duration = TimeSpan.FromMilliseconds(50),
        ExecutedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task AnalyzeFailure_DisabledByOption_ReturnsEmpty()
    {
        ProliferationOptions disabledOpts = new() { EnableFailureFeedback = false };
        DefaultFailureAnalyzer analyzer = new(_brain.Object, disabledOpts);

        IReadOnlyList<NeuronScenario> result = await analyzer.AnalyzeFailureAsync(
            CreateFailedScenario(), CreateFailureResult(), "{}", new ProliferationContext { RemainingBudget = 10 });

        result.Should().BeEmpty();
        _brain.Verify(b => b.AnalyzeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JsonElement?>(),
            It.IsAny<JsonElement?>(), It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeFailure_ZeroBudget_ReturnsEmpty()
    {
        DefaultFailureAnalyzer analyzer = CreateAnalyzer();

        IReadOnlyList<NeuronScenario> result = await analyzer.AnalyzeFailureAsync(
            CreateFailedScenario(), CreateFailureResult(), "{}", new ProliferationContext { RemainingBudget = 0 });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeFailure_BrainGeneratesScenarios_SetsParentId()
    {
        NeuronScenario child = new()
        {
            Id = "child-1",
            SeedRuleCode = "TEST",
            ScenarioName = "Follow-up probe",
            ProliferationReason = "probing failure boundary",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _brain.Setup(b => b.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationPlan
            {
                SeedRuleCode = "TEST",
                AiModelUsed = "test",
                Scenarios = [child]
            });

        DefaultFailureAnalyzer analyzer = CreateAnalyzer();
        NeuronScenario failedScenario = CreateFailedScenario();

        IReadOnlyList<NeuronScenario> result = await analyzer.AnalyzeFailureAsync(
            failedScenario, CreateFailureResult(), "{}", new ProliferationContext { RemainingBudget = 5 });

        result.Should().HaveCount(1);
        result[0].ParentScenarioId.Should().Be("failed-1");
        result[0].GenerationDepth.Should().Be(2); // parent depth (1) + 1
    }

    [Fact]
    public async Task AnalyzeFailure_RespectsBudgetCap()
    {
        List<NeuronScenario> children = [.. Enumerable.Range(0, 5).Select(i => new NeuronScenario
        {
            Id = $"child-{i}",
            SeedRuleCode = "TEST",
            ScenarioName = $"Probe {i}",
            ProliferationReason = "probing",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 0,
            CreatedAt = DateTimeOffset.UtcNow
        })];

        _brain.Setup(b => b.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationPlan
            {
                SeedRuleCode = "TEST",
                AiModelUsed = "test",
                Scenarios = children
            });

        DefaultFailureAnalyzer analyzer = CreateAnalyzer();

        IReadOnlyList<NeuronScenario> result = await analyzer.AnalyzeFailureAsync(
            CreateFailedScenario(), CreateFailureResult(), "{}", new ProliferationContext { RemainingBudget = 10 });

        result.Should().HaveCount(2); // MaxScenariosPerFailure = 2
    }

    [Fact]
    public async Task AnalyzeFailure_PassesErrorInfoToBrain()
    {
        _brain.Setup(b => b.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationPlan
            {
                SeedRuleCode = "TEST",
                AiModelUsed = "test",
                Scenarios = []
            });

        DefaultFailureAnalyzer analyzer = CreateAnalyzer();
        await analyzer.AnalyzeFailureAsync(
            CreateFailedScenario(), CreateFailureResult(), "{}", new ProliferationContext { RemainingBudget = 5 });

        // Verify the brain was called with non-null execution result (failure info)
        _brain.Verify(b => b.AnalyzeAsync(
            "TEST",
            "{}",
            It.Is<JsonElement?>(e => e.HasValue),
            null,
            It.Is<ProliferationContext>(c => c.FocusAreas != null && c.FocusAreas.Any(a => a.Contains("failure-boundary"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeFailure_BrainThrows_ReturnsEmpty()
    {
        _brain.Setup(b => b.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        DefaultFailureAnalyzer analyzer = CreateAnalyzer();

        IReadOnlyList<NeuronScenario> result = await analyzer.AnalyzeFailureAsync(
            CreateFailedScenario(), CreateFailureResult(), "{}", new ProliferationContext { RemainingBudget = 5 });

        result.Should().BeEmpty();
    }
}
