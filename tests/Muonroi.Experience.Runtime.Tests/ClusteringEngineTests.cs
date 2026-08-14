namespace Muonroi.Experience.Runtime.Tests;

/// <summary>
/// Tests for EVO-08 — ClusteringEngine Jaccard-based text clustering.
/// </summary>
[Trait("Category", "Evolution")]
public sealed class ClusteringEngineTests
{
    private static NeuronExperience MakeEntry(string trigger, string question = "test question?", string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = trigger,
        Question = question,
        Reasoning = [],
        Solution = "test solution",
        Tier = ExperienceTier.SelfQA,
        Confidence = 0.5f,
        CreatedFrom = "test",
        CreatedAt = DateTimeOffset.UtcNow
    };

    // -------------------------------------------------------------------------
    // Test 1: Empty input → empty result
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupBySimilarity_EmptyInput_ReturnsEmpty()
    {
        var result = ClusteringEngine.GroupBySimilarity([], 0.7f);
        result.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Test 2: Single entry → one cluster of one
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupBySimilarity_SingleEntry_ReturnsOneClusterOfOne()
    {
        var entry = MakeEntry("reading file before editing it");
        var result = ClusteringEngine.GroupBySimilarity([entry], 0.7f);

        result.Should().HaveCount(1);
        result[0].Should().ContainSingle(e => e.Id == entry.Id);
    }

    // -------------------------------------------------------------------------
    // Test 3: Identical entries → same cluster (Jaccard = 1.0)
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupBySimilarity_IdenticalTriggers_SameCluster()
    {
        // Same trigger tokens → Jaccard = 1.0 >= 0.7 threshold
        var e1 = MakeEntry("reading file before editing code", id: "id-1");
        var e2 = MakeEntry("reading file before editing code", id: "id-2");
        var e3 = MakeEntry("reading file before editing code", id: "id-3");

        var result = ClusteringEngine.GroupBySimilarity([e1, e2, e3], 0.7f);

        result.Should().HaveCount(1);
        result[0].Should().HaveCount(3);
    }

    // -------------------------------------------------------------------------
    // Test 4: Completely different words → separate clusters (Jaccard ≈ 0.0)
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupBySimilarity_CompletelyDifferentTriggers_SeparateClusters()
    {
        var e1 = MakeEntry("reading file before editing");
        var e2 = MakeEntry("database connection timeout retry");

        var result = ClusteringEngine.GroupBySimilarity([e1, e2], 0.7f);

        result.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // Test 5: Partial overlap below 0.7 → separate clusters
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupBySimilarity_LowOverlap_SeparateClusters()
    {
        // Tokens: "file" in common, rest different → Jaccard = 1/7 ≈ 0.14 < 0.7
        var e1 = MakeEntry("editing file without reading first");
        var e2 = MakeEntry("file permission denied wrong path");

        var result = ClusteringEngine.GroupBySimilarity([e1, e2], 0.7f);

        result.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // Test 6: High overlap above 0.7 → same cluster
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupBySimilarity_HighOverlap_SameCluster()
    {
        // High overlap: "reading file before editing code changes" vs "reading file before editing source changes"
        // shared: reading, file, before, editing, changes (5), union: reading, file, before, editing, code, source, changes (7)
        // Jaccard = 5/7 ≈ 0.71 >= 0.7
        var e1 = MakeEntry("reading file before editing code changes", id: "id-1");
        var e2 = MakeEntry("reading file before editing source changes", id: "id-2");

        var result = ClusteringEngine.GroupBySimilarity([e1, e2], 0.7f);

        result.Should().HaveCount(1);
        result[0].Should().HaveCount(2);
    }
}
