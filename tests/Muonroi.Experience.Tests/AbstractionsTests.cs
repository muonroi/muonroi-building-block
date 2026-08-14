namespace Muonroi.Experience.Tests;

[Trait("Category", "Abstractions")]
public sealed class AbstractionsTests
{
    [Fact]
    public void ABS01_NeuronExperience_RecordEquality_HoldsForSameValues()
    {
        var a = new NeuronExperience
        {
            Id = "test-1",
            Trigger = "retry loop",
            Question = "Why fail?",
            Reasoning = ["check output"],
            Solution = "Read error first",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.6f,
            CreatedFrom = "session-1",
            CreatedAt = new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero)
        };

        var b = new NeuronExperience
        {
            Id = "test-1",
            Trigger = "retry loop",
            Question = "Why fail?",
            Reasoning = ["check output"],
            Solution = "Read error first",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.6f,
            CreatedFrom = "session-1",
            CreatedAt = new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero)
        };

        // Record equality for records with array fields: use BeEquivalentTo for deep comparison
        a.Should().BeEquivalentTo(b, "records with identical field values should be structurally equal");
        // Also verify individual field access works
        a.Trigger.Should().Be(b.Trigger);
        a.Tier.Should().Be(b.Tier);
        a.Confidence.Should().Be(b.Confidence);
    }

    [Fact]
    public async Task ABS02_IExperienceStore_Mockable_VerifyStoreAsyncCalledOnce()
    {
        var storeMock = new Mock<IExperienceStore>();
        storeMock
            .Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var entry = new NeuronExperience
        {
            Id = "mock-1",
            Trigger = "test trigger",
            Question = "test question",
            Reasoning = ["step1"],
            Solution = "test solution",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.5f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await storeMock.Object.StoreAsync(entry);

        result.Should().BeTrue();
        storeMock.Verify(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
