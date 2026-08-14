namespace Muonroi.Experience.Runtime.Tests;

[Trait("Category", "Extraction")]
public sealed class CompositeExperienceBrainTests
{
    private static NeuronExperience MakeEntry(string trigger) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Trigger = trigger,
        Question = $"Why did {trigger} happen?",
        Reasoning = ["checked logs", "traced call"],
        Solution = $"Fix: address {trigger}",
        Tier = ExperienceTier.SelfQA,
        Confidence = 0.5f,
        CreatedFrom = "composite-test",
        CreatedAt = DateTimeOffset.UtcNow
    };

    // -------------------------------------------------------------------------
    // Test 1: Primary returns result -> fallback NOT called
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_PrimarySucceeds_ReturnsPrimaryResult_FallbackNotCalled()
    {
        var primaryMock = new Mock<IExperienceBrain>();
        var fallbackMock = new Mock<IExperienceBrain>();
        var entry = MakeEntry("retry loop");

        primaryMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        fallbackMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var composite = new CompositeExperienceBrain(primaryMock.Object, fallbackMock.Object);

        IEnumerable<NeuronExperience> result = await composite.ExtractAsync("session log");

        result.Should().ContainSingle(e => e.Id == entry.Id);
        fallbackMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 2: Primary returns empty -> fallback called
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_PrimaryReturnsEmpty_FallbackCalled()
    {
        var primaryMock = new Mock<IExperienceBrain>();
        var fallbackMock = new Mock<IExperienceBrain>();
        var fallbackEntry = MakeEntry("fallback trigger");

        primaryMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        fallbackMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([fallbackEntry]);

        var composite = new CompositeExperienceBrain(primaryMock.Object, fallbackMock.Object);

        IEnumerable<NeuronExperience> result = await composite.ExtractAsync("session log");

        result.Should().ContainSingle(e => e.Id == fallbackEntry.Id);
        fallbackMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 3: Primary throws -> fallback called
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_PrimaryThrows_FallbackCalled()
    {
        var primaryMock = new Mock<IExperienceBrain>();
        var fallbackMock = new Mock<IExperienceBrain>();
        var fallbackEntry = MakeEntry("fallback after exception");

        primaryMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));
        fallbackMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([fallbackEntry]);

        var composite = new CompositeExperienceBrain(primaryMock.Object, fallbackMock.Object);

        IEnumerable<NeuronExperience> result = await composite.ExtractAsync("session log");

        result.Should().ContainSingle(e => e.Id == fallbackEntry.Id);
        fallbackMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 4: Both primary and fallback throw -> returns empty, no exception
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_BothThrow_ReturnsEmpty_NoException()
    {
        var primaryMock = new Mock<IExperienceBrain>();
        var fallbackMock = new Mock<IExperienceBrain>();

        primaryMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));
        fallbackMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fallback failed"));

        var composite = new CompositeExperienceBrain(primaryMock.Object, fallbackMock.Object);

        Func<Task> act = async () =>
        {
            IEnumerable<NeuronExperience> result = await composite.ExtractAsync("session log");
            result.Should().BeEmpty();
        };

        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // Test 5: Both return empty -> returns empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_BothReturnEmpty_ReturnsEmpty()
    {
        var primaryMock = new Mock<IExperienceBrain>();
        var fallbackMock = new Mock<IExperienceBrain>();

        primaryMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        fallbackMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var composite = new CompositeExperienceBrain(primaryMock.Object, fallbackMock.Object);

        IEnumerable<NeuronExperience> result = await composite.ExtractAsync("session log");

        result.Should().BeEmpty();
    }
}
