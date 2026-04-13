using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;
using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class VectorSemanticDeduplicatorTests
{
    private static NeuronScenario CreateScenario(string id, string name, string? expected = null)
    {
        using JsonDocument doc = JsonDocument.Parse("""{"amount": 100}""");
        return new NeuronScenario
        {
            Id = id,
            SeedRuleCode = "TEST",
            ScenarioName = name,
            ProliferationReason = "test",
            InputFacts = doc.RootElement.Clone(),
            ExpectedBehavior = expected ?? "should work",
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectors_Returns1()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [1f, 0f, 0f];

        double sim = VectorSemanticDeduplicator.CosineSimilarity(a, b);
        sim.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_Returns0()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [0f, 1f, 0f];

        double sim = VectorSemanticDeduplicator.CosineSimilarity(a, b);
        sim.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_Returns0()
    {
        double sim = VectorSemanticDeduplicator.CosineSimilarity([], []);
        sim.Should().Be(0);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_Returns0()
    {
        float[] a = [1f, 0f];
        float[] b = [1f, 0f, 0f];

        double sim = VectorSemanticDeduplicator.CosineSimilarity(a, b);
        sim.Should().Be(0);
    }

    [Fact]
    public void Deduplicate_WithEmptyEmbedder_FallsBackToHashDedup()
    {
        // Embedder that always returns empty — semantic dedup skips
        var embedder = new FakeEmbedder([]);
        var options = new ProliferationOptions { SemanticDedupThreshold = 0.85 };
        var hashDedup = new InputHashDeduplicator();
        var dedup = new VectorSemanticDeduplicator(embedder, options, hashDedup);

        // Use different inputFacts so hash dedup doesn't catch them
        using JsonDocument docA = JsonDocument.Parse("""{"amount": 100}""");
        using JsonDocument docB = JsonDocument.Parse("""{"amount": 200}""");
        List<NeuronScenario> candidates =
        [
            new NeuronScenario { Id = "1", SeedRuleCode = "TEST", ScenarioName = "test A unique name alpha",
                ProliferationReason = "r", InputFacts = docA.RootElement.Clone(), Status = ScenarioStatus.Pending, CreatedAt = DateTimeOffset.UtcNow },
            new NeuronScenario { Id = "2", SeedRuleCode = "TEST", ScenarioName = "test B unique name beta",
                ProliferationReason = "r", InputFacts = docB.RootElement.Clone(), Status = ScenarioStatus.Pending, CreatedAt = DateTimeOffset.UtcNow }
        ];

        IReadOnlyList<NeuronScenario> result = dedup.Deduplicate(candidates, []);
        result.Should().HaveCount(2); // Both kept since embedder returns empty
    }

    private sealed class FakeEmbedder(float[] returnValue) : IVectorEmbedder
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(returnValue);
    }
}
