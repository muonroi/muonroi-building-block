using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime;
using Muonroi.Experience.Runtime.Qdrant;
using Qdrant.Client.Grpc;
using System.Text.Json;
using Xunit;

namespace Muonroi.Experience.Runtime.Tests;

public sealed class QdrantExperienceStoreTests
{
    private static IOptions<ExperienceStoreOptions> Opts(int vectorSize = 4) =>
        Options.Create(new ExperienceStoreOptions
        {
            VectorSize = vectorSize,
            StoreType = ExperienceStoreType.Qdrant,
            Budget = new ExperienceBudgetConfig()
        });

    // -------------------------------------------------------------------------
    // Test 1: Constructor VectorSize=0 throws ArgumentException
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_VectorSizeZero_ThrowsArgumentException()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        var opts = Options.Create(new ExperienceStoreOptions { VectorSize = 0 });

        Action act = () => _ = new QdrantExperienceStore(mockWrapper.Object, opts);

        act.Should().Throw<ArgumentException>().WithMessage("*VectorSize*");
    }

    // -------------------------------------------------------------------------
    // Test 2: StoreAsync calls UpsertAsync with correct collection name
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StoreAsync_CallsUpsertWithCorrectCollection()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();

        // ScrollAsync returns empty — no existing entries (for budget check).
        mockWrapper
            .Setup(w => w.ScrollAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RetrievedPoint>());

        // CreateCollectionAsync is a no-op.
        mockWrapper
            .Setup(w => w.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockWrapper
            .Setup(w => w.UpsertAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = new QdrantExperienceStore(mockWrapper.Object, Opts());
        NeuronExperience entry = MakeEntry(ExperienceTier.Behavioral);

        bool result = await store.StoreAsync(entry);

        result.Should().BeTrue();
        mockWrapper.Verify(
            w => w.UpsertAsync(
                "experience-behavioral",
                It.IsAny<IReadOnlyList<PointStruct>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 3: FindRelevantAsync with plain text query returns empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindRelevantAsync_PlainTextQuery_ReturnsEmpty()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        mockWrapper
            .Setup(w => w.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = new QdrantExperienceStore(mockWrapper.Object, Opts());

        IEnumerable<ExperienceSearchResult> results = await store.FindRelevantAsync("some plain text query");

        results.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Test 4: FindRelevantAsync_ReturnsCorrectTop5_ByCosineScore (STO-02)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindRelevantAsync_ReturnsCorrectTop5_ByCosineScore()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        mockWrapper
            .Setup(w => w.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Build mock NeuronExperience entries for each collection.
        NeuronExperience[] principleEntries = Enumerable.Range(0, 5)
            .Select(i => MakeEntry(ExperienceTier.Principle, $"principle-{i}"))
            .ToArray();
        NeuronExperience[] behavioralEntries = Enumerable.Range(0, 3)
            .Select(i => MakeEntry(ExperienceTier.Behavioral, $"behavioral-{i}"))
            .ToArray();
        NeuronExperience[] selfqaEntries = Enumerable.Range(0, 2)
            .Select(i => MakeEntry(ExperienceTier.SelfQA, $"selfqa-{i}"))
            .ToArray();

        // Principle: scores [0.95, 0.80, 0.60, 0.40, 0.20]
        var principleHits = new[]
        {
            MakeScoredPoint(principleEntries[0], 0.95f),
            MakeScoredPoint(principleEntries[1], 0.80f),
            MakeScoredPoint(principleEntries[2], 0.60f),
            MakeScoredPoint(principleEntries[3], 0.40f),
            MakeScoredPoint(principleEntries[4], 0.20f)
        };

        // Behavioral: scores [0.90, 0.75, 0.55]
        var behavioralHits = new[]
        {
            MakeScoredPoint(behavioralEntries[0], 0.90f),
            MakeScoredPoint(behavioralEntries[1], 0.75f),
            MakeScoredPoint(behavioralEntries[2], 0.55f)
        };

        // SelfQA: scores [0.85, 0.70]
        var selfqaHits = new[]
        {
            MakeScoredPoint(selfqaEntries[0], 0.85f),
            MakeScoredPoint(selfqaEntries[1], 0.70f)
        };

        mockWrapper
            .Setup(w => w.SearchAsync("experience-principles", It.IsAny<float[]>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principleHits);

        mockWrapper
            .Setup(w => w.SearchAsync("experience-behavioral", It.IsAny<float[]>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(behavioralHits);

        mockWrapper
            .Setup(w => w.SearchAsync("experience-selfqa", It.IsAny<float[]>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(selfqaHits);

        var store = new QdrantExperienceStore(mockWrapper.Object, Opts(vectorSize: 4));

        // Valid 4-dim JSON float[] query.
        string query = "[0.1, 0.2, 0.3, 0.4]";
        IEnumerable<ExperienceSearchResult> results = await store.FindRelevantAsync(query, topK: 5);

        var resultList = results.ToList();
        resultList.Should().HaveCount(5);

        // Assert ordering: [0.95, 0.90, 0.85, 0.80, 0.75]
        float[] expectedScores = [0.95f, 0.90f, 0.85f, 0.80f, 0.75f];
        for (int i = 0; i < expectedScores.Length; i++)
        {
            resultList[i].RelevanceScore.Should().BeApproximately(expectedScores[i], 0.001f,
                $"result at index {i} should have score {expectedScores[i]}");
        }

        // Assert NeuronExperience is correctly deserialized.
        resultList.Should().AllSatisfy(r => r.Experience.Should().NotBeNull());
        resultList.Should().AllSatisfy(r => r.Experience.Id.Should().NotBeNullOrEmpty());
    }

    // -------------------------------------------------------------------------
    // Test 5: QdrantPayload_SelfQA_RoundTrip (STO-05 for Qdrant)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QdrantPayload_SelfQA_RoundTrip()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        mockWrapper
            .Setup(w => w.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // ScrollAsync returns empty for budget check.
        mockWrapper
            .Setup(w => w.ScrollAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RetrievedPoint>());

        // Capture the upserted point.
        PointStruct? capturedPoint = null;
        mockWrapper
            .Setup(w => w.UpsertAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<PointStruct>, CancellationToken>((_, pts, _) =>
            {
                capturedPoint = pts.FirstOrDefault();
            })
            .Returns(Task.CompletedTask);

        var store = new QdrantExperienceStore(mockWrapper.Object, Opts());

        var original = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "missing null check",
            Question = "How to fix?",
            Reasoning = ["Step 1", "Step 2"],
            Solution = "Read first",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.5f,
            CreatedFrom = "session-123",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await store.StoreAsync(original);

        capturedPoint.Should().NotBeNull();
        capturedPoint!.Payload.Should().ContainKey("json");

        string jsonPayload = capturedPoint.Payload["json"].StringValue;
        NeuronExperience? loaded = JsonSerializer.Deserialize<NeuronExperience>(jsonPayload);

        loaded.Should().NotBeNull();
        loaded!.Question.Should().Be("How to fix?");
        loaded.Reasoning.Should().Equal("Step 1", "Step 2");
        loaded.Solution.Should().Be("Read first");
        loaded.CreatedFrom.Should().Be("session-123");
    }

    // -------------------------------------------------------------------------
    // Test 6: StoreAsync budget exceeded returns false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StoreAsync_BudgetExceeded_LogsWarningAndReturnsFalse()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        mockWrapper
            .Setup(w => w.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Return existing entries that fill the budget (large content).
        var existingFiller = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = new string('X', 800),
            Question = new string('X', 800),
            Reasoning = [new string('X', 400)],
            Solution = new string('X', 400),
            Tier = ExperienceTier.Principle,
            Confidence = 0.5f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var fillerPoint = new RetrievedPoint { Id = new PointId { Uuid = existingFiller.Id } };
        fillerPoint.Payload["json"] = JsonSerializer.Serialize(existingFiller);

        mockWrapper
            .Setup(w => w.ScrollAsync("experience-principles", It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fillerPoint });

        var opts = Options.Create(new ExperienceStoreOptions
        {
            VectorSize = 4,
            Budget = new ExperienceBudgetConfig { PrincipleBudget = 10 } // tiny budget
        });

        var store = new QdrantExperienceStore(mockWrapper.Object, opts);

        NeuronExperience newEntry = MakeEntry(ExperienceTier.Principle, "overflow-entry");
        bool result = await store.StoreAsync(newEntry);

        result.Should().BeFalse();
        // UpsertAsync should NOT have been called.
        mockWrapper.Verify(
            w => w.UpsertAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NeuronExperience MakeEntry(ExperienceTier tier, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = "test trigger",
        Question = "test question?",
        Reasoning = ["reason"],
        Solution = "test solution",
        Tier = tier,
        Confidence = 0.5f,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static ScoredPoint MakeScoredPoint(NeuronExperience entry, float score)
    {
        var point = new ScoredPoint
        {
            Id = new PointId { Uuid = entry.Id },
            Score = score
        };
        point.Payload["json"] = JsonSerializer.Serialize(entry);
        return point;
    }
}
