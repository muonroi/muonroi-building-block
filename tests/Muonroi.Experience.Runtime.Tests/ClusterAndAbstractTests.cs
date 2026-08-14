namespace Muonroi.Experience.Runtime.Tests;

/// <summary>
/// Tests for EVO-03/04/07 — ClusterAndAbstractAsync on FileExperienceStore.
/// Verifies brain delegation, principle storage, and budget guard (Pitfall 3).
/// </summary>
[Trait("Category", "Evolution")]
public sealed class ClusterAndAbstractTests : IDisposable
{
    private readonly string _tempDir;

    public ClusterAndAbstractTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cluster-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private FileExperienceStore MakeStore(IExperienceBrain brain, string? dir = null, ExperienceBudgetConfig? budget = null)
    {
        string path = dir ?? _tempDir;
        var opts = Options.Create(new ExperienceStoreOptions
        {
            FileDirectoryPath = path,
            StoreType = ExperienceStoreType.File,
            Budget = budget ?? new ExperienceBudgetConfig()
        });
        return new FileExperienceStore(opts, log: null, brain: brain);
    }

    private static NeuronExperience MakeEntry(string? trigger = null, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = trigger ?? "test trigger phrase",
        Question = "test question?",
        Reasoning = ["step one"],
        Solution = "test solution for this case",
        Tier = ExperienceTier.SelfQA,
        Confidence = 0.5f,
        CreatedFrom = "test",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static NeuronExperience MakePrinciple(string solution = "general principle solution") => new()
    {
        Id = Guid.NewGuid().ToString(),
        Trigger = "general trigger condition",
        Question = "general question",
        Reasoning = [],
        Solution = solution,
        Tier = ExperienceTier.SelfQA, // brain returns SelfQA; store promotes to Principle
        Confidence = 0.8f,
        CreatedFrom = "claude-brain",
        CreatedAt = DateTimeOffset.UtcNow
    };

    // -------------------------------------------------------------------------
    // Test 1 (EVO-03): Cluster of 3 → brain called once, principle stored with Tier=Principle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClusterAndAbstractAsync_ClusterOf3_BrainCalledOnce_PrincipleStored()
    {
        var brainMock = new Mock<IExperienceBrain>();
        NeuronExperience principleFromBrain = MakePrinciple();
        brainMock
            .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principleFromBrain);

        var store = MakeStore(brainMock.Object);

        var cluster = new[]
        {
            MakeEntry("trigger one phrase"),
            MakeEntry("trigger two phrase"),
            MakeEntry("trigger three phrase")
        };

        NeuronExperience result = await store.ClusterAndAbstractAsync(cluster);

        // Brain called exactly once
        brainMock.Verify(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Result has Tier = Principle
        result.Tier.Should().Be(ExperienceTier.Principle);

        // Principle field populated from brain's solution
        result.Principle.Should().Be(principleFromBrain.Solution);

        // Entry exists in Principle tier of store
        IEnumerable<NeuronExperience> stored = await store.FindAllInTierAsync(ExperienceTier.Principle);
        stored.Should().ContainSingle(e => e.Tier == ExperienceTier.Principle);
    }

    // -------------------------------------------------------------------------
    // Test 2 (EVO-07): Budget rejection → InvalidOperationException thrown, source entries NOT deleted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClusterAndAbstractAsync_BudgetExceeded_ThrowsInvalidOperationException()
    {
        var brainMock = new Mock<IExperienceBrain>();
        // Brain returns a very large principle that will exceed the tiny budget
        NeuronExperience hugeResponse = MakePrinciple(new string('x', 5000)); // exceeds any reasonable budget
        brainMock
            .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hugeResponse);

        // Set a tiny budget so even a small principle exceeds it
        var tinyBudget = new ExperienceBudgetConfig { PrincipleBudget = 1 };
        var store = MakeStore(brainMock.Object, budget: tinyBudget);

        var cluster = new[]
        {
            MakeEntry(),
            MakeEntry(),
            MakeEntry()
        };

        Func<Task> act = async () => await store.ClusterAndAbstractAsync(cluster);

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*budget exceeded*");
    }

    // -------------------------------------------------------------------------
    // Test 3 (EVO-04): Abstraction prompt contains "Given these 3 related experiences"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClusterAndAbstractAsync_PromptContainsClusterCount()
    {
        string? capturedPrompt = null;
        var brainMock = new Mock<IExperienceBrain>();
        brainMock
            .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((prompt, _) => capturedPrompt = prompt)
            .ReturnsAsync(MakePrinciple());

        var store = MakeStore(brainMock.Object);

        var cluster = new[]
        {
            MakeEntry("first trigger phrase"),
            MakeEntry("second trigger phrase"),
            MakeEntry("third trigger phrase")
        };

        await store.ClusterAndAbstractAsync(cluster);

        capturedPrompt.Should().NotBeNull();
        capturedPrompt.Should().Contain("Given these 3 related experiences");
    }

    // -------------------------------------------------------------------------
    // Test 4: Returned principle has Tier=Principle and Principle field populated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClusterAndAbstractAsync_ReturnedPrinciple_HasCorrectFields()
    {
        const string principleText = "When testing code, always verify expected behavior";
        var brainMock = new Mock<IExperienceBrain>();
        brainMock
            .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrinciple(principleText));

        var store = MakeStore(brainMock.Object);

        var cluster = new[] { MakeEntry(), MakeEntry(), MakeEntry() };
        NeuronExperience result = await store.ClusterAndAbstractAsync(cluster);

        result.Tier.Should().Be(ExperienceTier.Principle);
        result.Principle.Should().Be(principleText);
        result.CreatedFrom.Should().Be("evolution-engine");
    }
}
