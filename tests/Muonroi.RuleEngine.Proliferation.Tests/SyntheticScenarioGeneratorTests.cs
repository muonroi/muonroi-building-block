using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class SyntheticScenarioGeneratorTests
{
    private static readonly ProliferationContext DefaultContext = new() { RemainingBudget = 10 };
    private readonly SyntheticScenarioGenerator _generator = new();

    [Fact]
    public void Generate_WithTwoInputFields_ReturnsAtLeastFourScenarios()
    {
        // Schema with 2 input fields → at least 4 boundary scenarios (null, max, min, empty per field)
        RuleSetSchema schema = new(
            RuleSetKind.CodeBased,
            [
                new FieldSchema("amount", "number", true, null),
                new FieldSchema("currency", "string", false, null)
            ],
            []);

        IReadOnlyList<NeuronScenario> result = _generator.Generate("SEED", schema, DefaultContext);

        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Generate_WithEmptySchema_ReturnsAtLeastOneScenario()
    {
        // Even with no schema fields, always return 1 scenario (empty inputFacts)
        RuleSetSchema schema = new(RuleSetKind.CodeBased, [], []);

        IReadOnlyList<NeuronScenario> result = _generator.Generate("SEED", schema, DefaultContext);

        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Generate_AllScenariosHaveSeedRuleCode()
    {
        RuleSetSchema schema = new(
            RuleSetKind.CodeBased,
            [new FieldSchema("amount", "number", true, null)],
            []);

        IReadOnlyList<NeuronScenario> result = _generator.Generate("MY_RULE", schema, DefaultContext);

        result.Should().AllSatisfy(s => s.SeedRuleCode.Should().Be("MY_RULE"));
    }

    [Fact]
    public void Generate_AllScenariosHaveSyntheticFallbackReason()
    {
        RuleSetSchema schema = new(
            RuleSetKind.CodeBased,
            [new FieldSchema("price", "number", true, null)],
            []);

        IReadOnlyList<NeuronScenario> result = _generator.Generate("SEED", schema, DefaultContext);

        result.Should().AllSatisfy(s =>
            s.ProliferationReason.Should().Contain("Synthetic boundary case"));
    }

    [Fact]
    public void Generate_AllScenariosAreTechnicalType()
    {
        RuleSetSchema schema = new(
            RuleSetKind.CodeBased,
            [new FieldSchema("age", "number", false, null)],
            []);

        IReadOnlyList<NeuronScenario> result = _generator.Generate("SEED", schema, DefaultContext);

        result.Should().AllSatisfy(s => s.Type.Should().Be(ScenarioType.Technical));
    }
}
