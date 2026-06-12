using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class InputHashDeduplicatorTests
{
    private readonly InputHashDeduplicator _deduplicator = new();

    private static NeuronScenario CreateScenario(string id, string name, string inputJson) => new()
    {
        Id = id,
        SeedRuleCode = "TEST",
        ScenarioName = name,
        ProliferationReason = "test",
        Status = ScenarioStatus.Pending,
        GenerationDepth = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        InputFacts = JsonDocument.Parse(inputJson).RootElement.Clone()
    };

    [Fact]
    public void Deduplicate_EmptyCandidates_ReturnsEmpty()
    {
        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate([], []);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_NoDuplicates_ReturnsAll()
    {
        List<NeuronScenario> candidates =
        [
            CreateScenario("1", "Test A", """{"amount": 100}"""),
            CreateScenario("2", "Test B", """{"amount": 200}""")
        ];

        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate(candidates, []);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Deduplicate_ExactInputDuplicate_Filtered()
    {
        NeuronScenario existing = CreateScenario("existing-1", "Zero amount test", """{"amount": 0}""");
        NeuronScenario candidate = CreateScenario("new-1", "Zero boundary check", """{"amount": 0}""");

        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate([candidate], [existing]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_DifferentKeyOrder_SameHash()
    {
        NeuronScenario existing = CreateScenario("existing-1", "Test 1", """{"a": 1, "b": 2}""");
        NeuronScenario candidate = CreateScenario("new-1", "Test different order", """{"b": 2, "a": 1}""");

        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate([candidate], [existing]);
        result.Should().BeEmpty(); // Same normalized JSON
    }

    [Fact]
    public void Deduplicate_NameDuplicate_Filtered()
    {
        NeuronScenario existing = CreateScenario("existing-1", "Fee multiplier at zero", """{"fee": 0}""");
        // Different inputs but same normalized name
        NeuronScenario candidate = CreateScenario("new-1", "Fee Multiplier At Zero!", """{"fee": 0.0}""");

        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate([candidate], [existing]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_IntraBatchDuplicates_Filtered()
    {
        List<NeuronScenario> candidates =
        [
            CreateScenario("1", "Test A", """{"amount": 100}"""),
            CreateScenario("2", "Test B", """{"amount": 100}"""), // Same input as first
            CreateScenario("3", "Test C", """{"amount": 200}""")
        ];

        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate(candidates, []);
        result.Should().HaveCount(2);
        result.Select(s => s.Id).Should().Contain("1").And.Contain("3");
    }

    [Fact]
    public void Deduplicate_DifferentValues_Kept()
    {
        NeuronScenario existing = CreateScenario("existing-1", "Amount 100", """{"amount": 100}""");
        NeuronScenario candidate = CreateScenario("new-1", "Amount 101", """{"amount": 101}""");

        IReadOnlyList<NeuronScenario> result = _deduplicator.Deduplicate([candidate], [existing]);
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ComputeInputHash_UndefinedElement_ReturnsEmpty()
    {
        string hash = InputHashDeduplicator.ComputeInputHash(default);
        hash.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeName_StripsPunctuationAndLowercases()
    {
        string normalized = InputHashDeduplicator.NormalizeName("Fee Multiplier at Zero!");
        normalized.Should().Be("feemultiplieratzero");
    }

    [Fact]
    public void NormalizeName_EmptyString_ReturnsEmpty()
    {
        string normalized = InputHashDeduplicator.NormalizeName("");
        normalized.Should().BeEmpty();
    }
}
