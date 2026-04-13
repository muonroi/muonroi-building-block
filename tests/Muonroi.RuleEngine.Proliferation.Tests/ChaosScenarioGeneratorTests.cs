using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ChaosScenarioGeneratorTests
{
    private readonly DefaultChaosScenarioGenerator _generator = new();

    private static readonly RuleSetSchema TestSchema = new(
        RuleSetKind.DecisionTable,
        InputFields:
        [
            new FieldSchema("amount", "number", true, null),
            new FieldSchema("currency", "string", true, null),
            new FieldSchema("region", "string", false, null)
        ],
        OutputFields: []);

    [Fact]
    public void GenerateChaosScenarios_ProducesNullInjection()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 20);

        chaos.Should().Contain(s => s.ScenarioName.Contains("null amount"));
        chaos.Should().Contain(s => s.ScenarioName.Contains("null currency"));
    }

    [Fact]
    public void GenerateChaosScenarios_ProducesOverflowValues()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 20);

        chaos.Should().Contain(s => s.ScenarioName.Contains("overflow amount"));
    }

    [Fact]
    public void GenerateChaosScenarios_ProducesTypeCoercion()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 20);

        chaos.Should().Contain(s => s.ScenarioName.Contains("type coercion"));
    }

    [Fact]
    public void GenerateChaosScenarios_AllAreTechnicalType()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 20);

        chaos.Should().AllSatisfy(s => s.Type.Should().Be(ScenarioType.Technical));
    }

    [Fact]
    public void GenerateChaosScenarios_ScenarioNameStartsWithChaos()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 20);

        chaos.Should().AllSatisfy(s => s.ScenarioName.Should().StartWith("Chaos:"));
    }

    [Fact]
    public void GenerateChaosScenarios_RespectsBudget()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 3);

        chaos.Count.Should().BeInRange(0, 3);
    }

    [Fact]
    public void GenerateChaosScenarios_EmptySchema_ReturnsEmpty()
    {
        RuleSetSchema empty = new(RuleSetKind.Unknown, [], []);
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", empty, 10);

        chaos.Should().BeEmpty();
    }

    [Fact]
    public void GenerateChaosScenarios_IncludesUnknownFieldsInjection()
    {
        IReadOnlyList<NeuronScenario> chaos = _generator.GenerateChaosScenarios("TEST", "{}", TestSchema, 20);

        chaos.Should().Contain(s => s.ScenarioName.Contains("unknown fields"));
    }
}
