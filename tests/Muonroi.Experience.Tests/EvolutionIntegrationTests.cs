namespace Muonroi.Experience.Tests;

[Trait("Category", "Evolution")]
public sealed class EvolutionIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileExperienceStore MakeStore(IExperienceBrain? brain = null)
    {
        return new FileExperienceStore(
            Options.Create(new ExperienceStoreOptions
            {
                FileDirectoryPath = _tempDir,
                StoreType = ExperienceStoreType.File
            }),
            log: null,
            brain: brain);
    }

    private static NeuronExperience MakeSelfQAEntry(string id, string trigger, string solution)
    {
        return new NeuronExperience
        {
            Id = id,
            Trigger = trigger,
            Question = "Why fail?",
            Reasoning = ["check output"],
            Solution = solution,
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.6f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task EVO01_PromoteAsync_MovesSelfQAToBehavioral()
    {
        var store = MakeStore();
        var entry = MakeSelfQAEntry("promo-1", "retry edit loop", "Read error first");
        await store.StoreAsync(entry);

        var promoted = await store.PromoteAsync(entry);

        promoted.Tier.Should().Be(ExperienceTier.Behavioral, "promoted entry should be in Behavioral tier");

        var behavioral = (await store.FindAllInTierAsync(ExperienceTier.Behavioral)).ToList();
        behavioral.Should().Contain(e => e.Id == "promo-1", "promoted entry should appear in Behavioral tier");

        var selfQa = (await store.FindAllInTierAsync(ExperienceTier.SelfQA)).ToList();
        selfQa.Should().NotContain(e => e.Id == "promo-1", "promoted entry should no longer be in SelfQA tier");
    }

    [Fact]
    public async Task EVO02_ClusterAndAbstract_ProducesPrincipleFromCluster()
    {
        var principleEntry = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "general pattern",
            Question = "What is the principle?",
            Reasoning = ["abstracted from cluster"],
            Solution = "Always read error before retry",
            Principle = "Always read error before retry",
            Tier = ExperienceTier.Principle,
            Confidence = 0.9f,
            CreatedFrom = "evolution",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var brainMock = new Mock<IExperienceBrain>();
        brainMock
            .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principleEntry);

        var store = MakeStore(brainMock.Object);

        var e1 = MakeSelfQAEntry("c1", "retry edit loop", "Read error first");
        var e2 = MakeSelfQAEntry("c2", "retry edit compile", "Read compile output");
        var e3 = MakeSelfQAEntry("c3", "retry edit build", "Read build error");
        await store.StoreAsync(e1);
        await store.StoreAsync(e2);
        await store.StoreAsync(e3);

        var principle = await store.ClusterAndAbstractAsync(new[] { e1, e2, e3 });

        principle.Should().NotBeNull();
        principle.Tier.Should().Be(ExperienceTier.Principle);

        // Manually delete source entries (as the orchestrator would do)
        await store.DeleteAsync("c1");
        await store.DeleteAsync("c2");
        await store.DeleteAsync("c3");

        var principles = (await store.FindAllInTierAsync(ExperienceTier.Principle)).ToList();
        principles.Should().ContainSingle(p => p.Tier == ExperienceTier.Principle,
            "one principle should exist after cluster-and-abstract");
    }

    [Fact]
    public async Task EVO03_MemoryShrinks_TotalTokensReducedAfterCluster()
    {
        // Small entries: ~10 tokens each (trigger + question + reasoning + solution) / 4
        var e1 = MakeSelfQAEntry("ms1", "retry edit", "Read error");
        var e2 = MakeSelfQAEntry("ms2", "retry edit", "Read error");
        var e3 = MakeSelfQAEntry("ms3", "retry edit", "Read error");

        // Brain returns SHORT principle — must be smaller than 3 source entries combined
        var shortPrinciple = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "error",
            Question = "Why?",
            Reasoning = ["read"],
            Solution = "Read",
            Principle = "Read error",
            Tier = ExperienceTier.Principle,
            Confidence = 0.9f,
            CreatedFrom = "evolution",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var brainMock = new Mock<IExperienceBrain>();
        brainMock
            .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shortPrinciple);

        var store = MakeStore(brainMock.Object);
        await store.StoreAsync(e1);
        await store.StoreAsync(e2);
        await store.StoreAsync(e3);

        // Measure tokens before evolution
        var selfQaBefore = (await store.FindAllInTierAsync(ExperienceTier.SelfQA)).ToList();
        int tokensBefore = selfQaBefore.Sum(TokenBudgetEnforcer.EstimateTokens);

        // Cluster and abstract
        await store.ClusterAndAbstractAsync(new[] { e1, e2, e3 });

        // Delete source entries (as orchestrator would)
        await store.DeleteAsync("ms1");
        await store.DeleteAsync("ms2");
        await store.DeleteAsync("ms3");

        // Measure tokens after evolution (only principle tier)
        var principlesAfter = (await store.FindAllInTierAsync(ExperienceTier.Principle)).ToList();
        int tokensAfter = principlesAfter.Sum(TokenBudgetEnforcer.EstimateTokens);

        tokensAfter.Should().BeLessThan(tokensBefore,
            "Evolution must compress entries into principle — memory should shrink as knowledge generalizes");
    }
}
