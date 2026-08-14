namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ScenarioInputValidatorTests
{
    private static readonly RuleSetSchema TestSchema = new(
        RuleSetKind.DecisionTable,
        InputFields:
        [
            new FieldSchema("amount", "number", true, null),
            new FieldSchema("currency", "string", true, null),
            new FieldSchema("region", "string", false, null)
        ],
        OutputFields: []);

    private static NeuronScenario CreateScenario(string inputJson)
    {
        using JsonDocument doc = JsonDocument.Parse(inputJson);
        return new NeuronScenario
        {
            Id = "test-1",
            SeedRuleCode = "TEST",
            ScenarioName = "test",
            ProliferationReason = "test",
            InputFacts = doc.RootElement.Clone(),
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsValid()
    {
        NeuronScenario scenario = CreateScenario("""{"amount": 100, "currency": "USD"}""");
        ValidationResult result = ScenarioInputValidator.Validate(scenario, TestSchema);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsInvalid()
    {
        NeuronScenario scenario = CreateScenario("""{"amount": 100}""");
        ValidationResult result = ScenarioInputValidator.Validate(scenario, TestSchema);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("currency"));
    }

    [Fact]
    public void Validate_UnknownFields_ReportsErrors()
    {
        NeuronScenario scenario = CreateScenario("""{"amount": 100, "currency": "USD", "foo": "bar"}""");
        ValidationResult result = ScenarioInputValidator.Validate(scenario, TestSchema);

        result.Errors.Should().Contain(e => e.Contains("foo"));
    }

    [Fact]
    public void Validate_MajorityUnknownFields_ReturnsInvalid()
    {
        NeuronScenario scenario = CreateScenario("""{"foo": 1, "bar": 2, "baz": 3}""");
        ValidationResult result = ScenarioInputValidator.Validate(scenario, TestSchema);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("hallucinated"));
    }

    [Fact]
    public void Validate_EmptySchema_AlwaysValid()
    {
        RuleSetSchema emptySchema = new(RuleSetKind.Unknown, [], []);
        NeuronScenario scenario = CreateScenario("""{"anything": true}""");
        ValidationResult result = ScenarioInputValidator.Validate(scenario, emptySchema);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonObjectInputFacts_ReturnsInvalid()
    {
        using JsonDocument doc = JsonDocument.Parse("42");
        NeuronScenario scenario = new()
        {
            Id = "test-2",
            SeedRuleCode = "TEST",
            ScenarioName = "test",
            ProliferationReason = "test",
            InputFacts = doc.RootElement.Clone(),
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        ValidationResult result = ScenarioInputValidator.Validate(scenario, TestSchema);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("inputFacts is not a JSON object");
    }

    [Fact]
    public void Validate_CaseInsensitiveFieldMatching()
    {
        NeuronScenario scenario = CreateScenario("""{"Amount": 100, "CURRENCY": "USD"}""");
        ValidationResult result = ScenarioInputValidator.Validate(scenario, TestSchema);

        result.IsValid.Should().BeTrue();
    }
}
