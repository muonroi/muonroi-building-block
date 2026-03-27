using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class CoverageTrackerTests
{
    private readonly DefaultCoverageTracker _tracker = new();

    private static readonly RuleSetSchema TestSchema = new(
        RuleSetKind.DecisionTable,
        InputFields:
        [
            new FieldSchema("amount", "number", true, null),
            new FieldSchema("currency", "string", true, null),
            new FieldSchema("region", "string", false, null),
            new FieldSchema("customerId", "string", false, null)
        ],
        OutputFields: []);

    private static NeuronScenario CreateScenario(string inputJson)
    {
        using JsonDocument doc = JsonDocument.Parse(inputJson);
        return new NeuronScenario
        {
            Id = Guid.NewGuid().ToString("N"),
            SeedRuleCode = "TEST",
            ScenarioName = "test",
            ProliferationReason = "test",
            InputFacts = doc.RootElement.Clone(),
            Status = ScenarioStatus.Passed,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void GetCoverage_FullCoverage_Returns100Percent()
    {
        List<NeuronScenario> scenarios =
        [
            CreateScenario("""{"amount": 100, "currency": "USD", "region": "US", "customerId": "C001"}""")
        ];

        CoverageReport report = _tracker.GetCoverage("TEST", scenarios, TestSchema);

        report.CoveragePercentage.Should().Be(100);
        report.CoveredFields.Should().HaveCount(4);
        report.UncoveredFields.Should().BeEmpty();
    }

    [Fact]
    public void GetCoverage_PartialCoverage_ReportsUncoveredFields()
    {
        List<NeuronScenario> scenarios =
        [
            CreateScenario("""{"amount": 100, "currency": "USD"}""")
        ];

        CoverageReport report = _tracker.GetCoverage("TEST", scenarios, TestSchema);

        report.CoveragePercentage.Should().Be(50);
        report.CoveredFields.Should().HaveCount(2);
        report.UncoveredFields.Should().Contain("region");
        report.UncoveredFields.Should().Contain("customerId");
    }

    [Fact]
    public void GetCoverage_EmptyScenarios_ReturnsZeroCoverage()
    {
        CoverageReport report = _tracker.GetCoverage("TEST", [], TestSchema);

        report.CoveragePercentage.Should().Be(0);
        report.UncoveredFields.Should().HaveCount(4);
    }

    [Fact]
    public void GetCoverage_EmptySchema_ReturnsZero()
    {
        RuleSetSchema emptySchema = new(RuleSetKind.Unknown, [], []);
        CoverageReport report = _tracker.GetCoverage("TEST", [], emptySchema);

        report.CoveragePercentage.Should().Be(0);
        report.CoveredFields.Should().BeEmpty();
    }

    [Fact]
    public void GetCoverage_MultipleScenarios_AccumulatesCoverage()
    {
        List<NeuronScenario> scenarios =
        [
            CreateScenario("""{"amount": 100}"""),
            CreateScenario("""{"currency": "USD"}"""),
            CreateScenario("""{"region": "EU", "customerId": "C002"}""")
        ];

        CoverageReport report = _tracker.GetCoverage("TEST", scenarios, TestSchema);

        report.CoveragePercentage.Should().Be(100);
        report.CoveredFields.Should().HaveCount(4);
    }
}
