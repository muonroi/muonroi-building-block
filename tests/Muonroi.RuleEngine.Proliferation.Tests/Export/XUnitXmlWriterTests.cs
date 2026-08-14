namespace Muonroi.RuleEngine.Proliferation.Tests.Export;

public class XUnitXmlWriterTests
{
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

    private static ScenarioResult MakeResult(string scenarioId, bool isSuccess, bool matchesExpectation = true, TimeSpan? duration = null, params string[] errors)
    {
        return new ScenarioResult
        {
            ScenarioId = scenarioId,
            IsSuccess = isSuccess,
            MatchesExpectation = matchesExpectation,
            Duration = duration ?? TimeSpan.FromMilliseconds(123),
            Errors = errors,
            ExecutedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void Write_EmptyScenarios_ReturnsValidXml_WithZeroCounts()
    {
        string xml = XUnitXmlWriter.Write("MyWorkflow", [], [], DateTimeOffset.UtcNow);

        xml.Should().NotBeNullOrWhiteSpace();
        xml.Should().Contain("<assemblies>");
        xml.Should().Contain("name=\"MyWorkflow\"");
        xml.Should().Contain("total=\"0\"");
        xml.Should().Contain("passed=\"0\"");

        // Verify it parses as valid XML
        var doc = new XmlDocument();
        doc.LoadXml(xml); // should not throw
    }

    [Fact]
    public void Write_PassedScenario_ShowsPassResult()
    {
        NeuronScenario scenario = MakeScenario("Happy path", ScenarioStatus.Passed);
        ScenarioResult result = MakeResult(scenario.Id, isSuccess: true, matchesExpectation: true);

        string xml = XUnitXmlWriter.Write("WF1", [scenario], [result], DateTimeOffset.UtcNow);

        xml.Should().Contain("result=\"Pass\"");
        xml.Should().Contain("name=\"Happy path\"");
        xml.Should().NotContain("<failure");
    }

    [Fact]
    public void Write_FailedScenario_ShowsFailResultWithFailureElement()
    {
        NeuronScenario scenario = MakeScenario("Null input", ScenarioStatus.Failed);
        ScenarioResult result = MakeResult(scenario.Id, isSuccess: false, matchesExpectation: false,
            errors: ["Field 'amount' is required", "Validation failed"]);

        string xml = XUnitXmlWriter.Write("WF1", [scenario], [result], DateTimeOffset.UtcNow);

        xml.Should().Contain("result=\"Fail\"");
        xml.Should().Contain("<failure");
        xml.Should().Contain("Field 'amount' is required");
    }

    [Fact]
    public void Write_SkippedScenario_ShowsSkipResult()
    {
        NeuronScenario scenario = MakeScenario("Skipped scenario", ScenarioStatus.Skipped);
        // No result = not executed = Skip

        string xml = XUnitXmlWriter.Write("WF1", [scenario], [], DateTimeOffset.UtcNow);

        xml.Should().Contain("result=\"Skip\"");
    }

    [Fact]
    public void Write_MultipleScenarios_ShowsCorrectCounts()
    {
        NeuronScenario s1 = MakeScenario("Pass scenario", ScenarioStatus.Passed);
        NeuronScenario s2 = MakeScenario("Fail scenario", ScenarioStatus.Failed);
        NeuronScenario s3 = MakeScenario("Also pass", ScenarioStatus.Passed);

        ScenarioResult r1 = MakeResult(s1.Id, isSuccess: true);
        ScenarioResult r2 = MakeResult(s2.Id, isSuccess: false, matchesExpectation: false, errors: ["Error A"]);
        ScenarioResult r3 = MakeResult(s3.Id, isSuccess: true);

        string xml = XUnitXmlWriter.Write("WF1", [s1, s2, s3], [r1, r2, r3], DateTimeOffset.UtcNow);

        xml.Should().Contain("total=\"3\"");
        xml.Should().Contain("passed=\"2\"");
        xml.Should().Contain("failed=\"1\"");

        var doc = new XmlDocument();
        doc.LoadXml(xml); // valid XML
    }
}
