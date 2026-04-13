using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ScenarioParserTests
{
    private static readonly ProliferationContext DefaultContext = new() { RemainingBudget = 10 };

    [Fact]
    public void Parse_ValidJsonArray_ReturnsScenarios()
    {
        string json = """
            [
                {
                    "scenario": "Negative amount",
                    "type": "business",
                    "reason": "Tests boundary",
                    "inputFacts": {"amount": -1},
                    "expectedBehavior": "should fail"
                }
            ]
            """;

        IReadOnlyList<NeuronScenario> result = ScenarioParser.Parse(json, "SEED", DefaultContext);

        result.Should().HaveCount(1);
        result[0].ScenarioName.Should().Be("Negative amount");
        result[0].Type.Should().Be(ScenarioType.Business);
        result[0].SeedRuleCode.Should().Be("SEED");
    }

    [Fact]
    public void Parse_MarkdownCodeFences_StripsAndParses()
    {
        string wrapped = """
            ```json
            [{"scenario":"Test","type":"technical","reason":"edge case","inputFacts":{},"expectedBehavior":"should pass"}]
            ```
            """;

        IReadOnlyList<NeuronScenario> result = ScenarioParser.Parse(wrapped, "SEED", DefaultContext);

        result.Should().HaveCount(1);
        result[0].ScenarioName.Should().Be("Test");
        result[0].Type.Should().Be(ScenarioType.Technical);
    }

    [Fact]
    public void Parse_SkipsIncompleteEntries()
    {
        string json = """
            [
                {"scenario":"Valid","type":"business","reason":"test","inputFacts":{}},
                {"scenario":"","reason":"missing name"},
                {"type":"technical","inputFacts":{}}
            ]
            """;

        IReadOnlyList<NeuronScenario> result = ScenarioParser.Parse(json, "SEED", DefaultContext);

        result.Should().HaveCount(1);
        result[0].ScenarioName.Should().Be("Valid");
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmpty()
    {
        IReadOnlyList<NeuronScenario> result = ScenarioParser.Parse("not json", "SEED", DefaultContext);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NonArray_ReturnsEmpty()
    {
        IReadOnlyList<NeuronScenario> result = ScenarioParser.Parse("{\"key\":\"value\"}", "SEED", DefaultContext);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SetsContextProperties()
    {
        string json = """[{"scenario":"Test","type":"business","reason":"test","inputFacts":{}}]""";
        var context = new ProliferationContext
        {
            Scope = ProliferationScope.Workflow,
            CurrentDepth = 2,
            RemainingBudget = 5,
            TenantId = "tenant-1"
        };

        IReadOnlyList<NeuronScenario> result = ScenarioParser.Parse(json, "SEED", context);

        result.Should().HaveCount(1);
        result[0].Scope.Should().Be(ProliferationScope.Workflow);
        result[0].GenerationDepth.Should().Be(2);
        result[0].TenantId.Should().Be("tenant-1");
        result[0].Status.Should().Be(ScenarioStatus.Pending);
    }
}
