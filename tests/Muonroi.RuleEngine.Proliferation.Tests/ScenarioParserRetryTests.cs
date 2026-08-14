namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ScenarioParserRetryTests
{
    private static readonly ProliferationContext DefaultContext = new() { RemainingBudget = 10 };

    private static readonly RuleSetSchema EmptySchema = new(RuleSetKind.CodeBased, [], []);

    private static readonly RuleSetSchema SimpleSchema = new(
        RuleSetKind.CodeBased,
        [new FieldSchema("amount", "number", true, null)],
        []);

    private static string ValidScenarioJson => """
        [
            {
                "scenario": "Positive amount",
                "type": "business",
                "reason": "Tests happy path",
                "inputFacts": {"amount": 100},
                "expectedBehavior": "should succeed"
            }
        ]
        """;

    [Fact]
    public async Task ParseWithRetry_FirstParseSucceeds_NoRetryNeeded()
    {
        // Arrange: AI returns valid JSON on first call
        int callCount = 0;
        Task<string?> AiCall(string prompt)
        {
            callCount++;
            return Task.FromResult<string?>(ValidScenarioJson);
        }

        Mock<ISyntheticScenarioGenerator> syntheticMock = new();

        // Act
        IReadOnlyList<NeuronScenario> result = await ScenarioParser.ParseWithRetry(
            AiCall, "SEED", DefaultContext, syntheticMock.Object, EmptySchema);

        // Assert
        result.Should().HaveCount(1);
        callCount.Should().Be(1);
        syntheticMock.Verify(s => s.Generate(It.IsAny<string>(), It.IsAny<RuleSetSchema>(), It.IsAny<ProliferationContext>()), Times.Never);
    }

    [Fact]
    public async Task ParseWithRetry_FirstParseEmpty_RetriesWithDifferentPrompt()
    {
        // Arrange: returns empty on first call, valid on second
        int callCount = 0;
        List<string> prompts = [];

        Task<string?> AiCall(string prompt)
        {
            callCount++;
            prompts.Add(prompt);
            if (callCount == 1) return Task.FromResult<string?>("[]");
            return Task.FromResult<string?>(ValidScenarioJson);
        }

        Mock<ISyntheticScenarioGenerator> syntheticMock = new();

        // Act
        IReadOnlyList<NeuronScenario> result = await ScenarioParser.ParseWithRetry(
            AiCall, "SEED", DefaultContext, syntheticMock.Object, EmptySchema);

        // Assert
        result.Should().HaveCount(1);
        callCount.Should().Be(2);
        // Second prompt should differ from first (contains retry instruction)
        prompts[1].Should().NotBe(prompts[0]);
        syntheticMock.Verify(s => s.Generate(It.IsAny<string>(), It.IsAny<RuleSetSchema>(), It.IsAny<ProliferationContext>()), Times.Never);
    }

    [Fact]
    public async Task ParseWithRetry_AllRetriesFail_FallsBackToSyntheticGenerator()
    {
        // Arrange: always returns empty
        Task<string?> AiCall(string _) => Task.FromResult<string?>("[]");

        List<NeuronScenario> syntheticScenarios =
        [
            new NeuronScenario
            {
                Id = "synth-1",
                SeedRuleCode = "SEED",
                ScenarioName = "Synthetic null amount",
                Type = ScenarioType.Technical,
                ProliferationReason = "Synthetic boundary case (AI fallback)",
                Status = ScenarioStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            }
        ];

        Mock<ISyntheticScenarioGenerator> syntheticMock = new();
        syntheticMock
            .Setup(s => s.Generate(It.IsAny<string>(), It.IsAny<RuleSetSchema>(), It.IsAny<ProliferationContext>()))
            .Returns(syntheticScenarios);

        // Act
        IReadOnlyList<NeuronScenario> result = await ScenarioParser.ParseWithRetry(
            AiCall, "SEED", DefaultContext, syntheticMock.Object, SimpleSchema, maxRetries: 3);

        // Assert
        result.Should().NotBeEmpty();
        syntheticMock.Verify(s => s.Generate("SEED", SimpleSchema, DefaultContext), Times.Once);
    }

    [Fact]
    public async Task ParseWithRetry_EachRetryUsesEscalatingPromptVariation()
    {
        // Arrange: always returns empty to force all retries
        List<string> receivedPrompts = [];
        Task<string?> AiCall(string prompt)
        {
            receivedPrompts.Add(prompt);
            return Task.FromResult<string?>("[]");
        }

        Mock<ISyntheticScenarioGenerator> syntheticMock = new();
        syntheticMock
            .Setup(s => s.Generate(It.IsAny<string>(), It.IsAny<RuleSetSchema>(), It.IsAny<ProliferationContext>()))
            .Returns([]);

        // Act
        await ScenarioParser.ParseWithRetry(
            AiCall, "SEED", DefaultContext, syntheticMock.Object, EmptySchema, maxRetries: 3);

        // Assert: 3 retry prompts are used (plus initial = 4 total calls max, or 3 retries on top of 1 initial)
        receivedPrompts.Count.Should().BeGreaterThanOrEqualTo(2);
        // Each retry should differ from the previous
        for (int i = 1; i < receivedPrompts.Count; i++)
        {
            receivedPrompts[i].Should().NotBe(receivedPrompts[0]);
        }
    }

    [Fact]
    public async Task ParseWithRetry_AlwaysReturnsAtLeastOneScenario()
    {
        // Arrange: null AI response every time, synthetic also generates at least 1
        Task<string?> AiCall(string _) => Task.FromResult<string?>(null);

        Mock<ISyntheticScenarioGenerator> syntheticMock = new();
        syntheticMock
            .Setup(s => s.Generate(It.IsAny<string>(), It.IsAny<RuleSetSchema>(), It.IsAny<ProliferationContext>()))
            .Returns([
                new NeuronScenario
                {
                    Id = "synth-fallback",
                    SeedRuleCode = "SEED",
                    ScenarioName = "Fallback scenario",
                    Type = ScenarioType.Technical,
                    ProliferationReason = "Synthetic boundary case (AI fallback)",
                    Status = ScenarioStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]);

        // Act
        IReadOnlyList<NeuronScenario> result = await ScenarioParser.ParseWithRetry(
            AiCall, "SEED", DefaultContext, syntheticMock.Object, EmptySchema);

        // Assert
        result.Should().NotBeEmpty();
    }
}
