using System.Text.Json;
using System.Xml;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Export;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests.Export;

public class TrxWriterTests
{
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static NeuronScenario MakeScenario(string name = "Test Scenario", ScenarioStatus status = ScenarioStatus.Passed)
    {
        using JsonDocument doc = JsonDocument.Parse("{}");
        return new NeuronScenario
        {
            Id = Guid.NewGuid().ToString("N"),
            SeedRuleCode = "RULE_001",
            ScenarioName = name,
            ProliferationReason = "test",
            InputFacts = doc.RootElement.Clone(),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ScenarioResult MakeResult(string scenarioId, bool isSuccess, bool matchesExpectation = true, params string[] errors)
    {
        return new ScenarioResult
        {
            ScenarioId = scenarioId,
            IsSuccess = isSuccess,
            MatchesExpectation = matchesExpectation,
            Duration = TimeSpan.FromMilliseconds(456),
            Errors = errors,
            ExecutedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void Write_PassedScenario_ShowsPassedOutcome()
    {
        NeuronScenario scenario = MakeScenario("Happy path");
        ScenarioResult result = MakeResult(scenario.Id, isSuccess: true);

        string trx = TrxWriter.Write("WF1", [scenario], [result], DateTimeOffset.UtcNow);

        trx.Should().Contain("outcome=\"Passed\"");
        trx.Should().Contain("testName=\"Happy path\"");
        trx.Should().NotContain("<ErrorInfo");

        var doc = new XmlDocument();
        doc.LoadXml(trx); // valid XML
    }

    [Fact]
    public void Write_FailedScenario_ShowsFailedOutcomeWithErrorInfo()
    {
        NeuronScenario scenario = MakeScenario("Null input");
        ScenarioResult result = MakeResult(scenario.Id, isSuccess: false, matchesExpectation: false,
            errors: ["Validation failed", "Required field missing"]);

        string trx = TrxWriter.Write("WF1", [scenario], [result], DateTimeOffset.UtcNow);

        trx.Should().Contain("outcome=\"Failed\"");
        trx.Should().Contain("<ErrorInfo");
        trx.Should().Contain("<Message");
        trx.Should().Contain("Validation failed");
    }

    [Fact]
    public void Write_HasCorrectTrxNamespace()
    {
        NeuronScenario scenario = MakeScenario("Namespace test");
        ScenarioResult result = MakeResult(scenario.Id, isSuccess: true);

        string trx = TrxWriter.Write("WF1", [scenario], [result], DateTimeOffset.UtcNow);

        trx.Should().Contain(TrxNamespace);

        // Parse as namespace-aware XML
        var doc = new XmlDocument();
        var nsManager = new XmlNamespaceManager(doc.NameTable);
        nsManager.AddNamespace("t", TrxNamespace);
        doc.LoadXml(trx);

        // Root element should be TestRun
        doc.DocumentElement!.LocalName.Should().Be("TestRun");

        // Should have Results element
        var results = doc.SelectNodes("//t:Results", nsManager);
        results.Should().NotBeNull();
        results!.Count.Should().BeGreaterThan(0);
    }
}
