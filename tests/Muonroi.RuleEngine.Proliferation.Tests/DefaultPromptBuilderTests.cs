using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class DefaultPromptBuilderTests
{
    private readonly DefaultPromptBuilder _builder = new();

    [Fact]
    public void BuildSystemPrompt_ContainsFewShotExamples()
    {
        string prompt = _builder.BuildSystemPrompt();

        prompt.Should().Contain("Example 1");
        prompt.Should().Contain("Example 2");
        prompt.Should().Contain("Order amount at zero boundary");
        prompt.Should().Contain("Extremely large numeric input");
    }

    [Fact]
    public void BuildSystemPrompt_ContainsJsonSchema()
    {
        string prompt = _builder.BuildSystemPrompt();

        prompt.Should().Contain("\"scenario\"");
        prompt.Should().Contain("\"type\"");
        prompt.Should().Contain("\"reason\"");
        prompt.Should().Contain("\"inputFacts\"");
        prompt.Should().Contain("\"expectedBehavior\"");
    }

    [Fact]
    public void BuildSystemPrompt_ContainsChainOfThoughtHint()
    {
        string prompt = _builder.BuildSystemPrompt();

        prompt.Should().Contain("Think step-by-step");
        prompt.Should().Contain("boundary values");
    }

    [Fact]
    public void BuildUserPrompt_IncludesBudgetAndRuleDefinition()
    {
        string prompt = _builder.BuildUserPrompt(
            """{"workflowName":"TEST"}""",
            executionResult: null,
            factBagSnapshot: null,
            budget: 5,
            focusAreas: null);

        prompt.Should().Contain("Generate 5 test scenarios");
        prompt.Should().Contain("Rule definition:");
        prompt.Should().Contain("TEST");
    }

    [Fact]
    public void BuildUserPrompt_IncludesOptionalSections()
    {
        using JsonDocument execDoc = JsonDocument.Parse("""{"result":"ok"}""");
        using JsonDocument factDoc = JsonDocument.Parse("""{"amount":100}""");

        string prompt = _builder.BuildUserPrompt(
            "{}",
            executionResult: execDoc.RootElement.Clone(),
            factBagSnapshot: factDoc.RootElement.Clone(),
            budget: 3,
            focusAreas: ["boundary", "null-handling"]);

        prompt.Should().Contain("Previous execution result:");
        prompt.Should().Contain("Previous FactBag state:");
        prompt.Should().Contain("Focus areas: boundary, null-handling");
    }

    [Fact]
    public void BuildUserPrompt_OmitsOptionalSectionsWhenNull()
    {
        string prompt = _builder.BuildUserPrompt("{}", null, null, 5, null);

        prompt.Should().NotContain("Previous execution result:");
        prompt.Should().NotContain("Previous FactBag state:");
        prompt.Should().NotContain("Focus areas:");
    }

    [Fact]
    public void BuildUserPrompt_InjectsSchemaSection()
    {
        RuleSetSchema schema = new(
            RuleSetKind.DecisionTable,
            InputFields:
            [
                new FieldSchema("amount", "number", true, null),
                new FieldSchema("currency", "string", true, "ISO 4217 code")
            ],
            OutputFields:
            [
                new FieldSchema("discount", "number", false, null)
            ]);

        string prompt = _builder.BuildUserPrompt("{}", null, null, 5, null, schema);

        prompt.Should().Contain("## Input Field Schema");
        prompt.Should().Contain("amount (number, required)");
        prompt.Should().Contain("currency (string, required)");
        prompt.Should().Contain("ISO 4217 code");
        prompt.Should().Contain("MUST use exactly these field names");
        prompt.Should().Contain("## Output Fields");
        prompt.Should().Contain("discount (number)");
    }

    [Fact]
    public void BuildUserPrompt_NoSchemaSection_WhenSchemaIsNull()
    {
        string prompt = _builder.BuildUserPrompt("{}", null, null, 5, null, null);

        prompt.Should().NotContain("## Input Field Schema");
        prompt.Should().NotContain("MUST use exactly these field names");
    }

    [Fact]
    public void BuildUserPrompt_NoSchemaSection_WhenSchemaIsEmpty()
    {
        RuleSetSchema emptySchema = new(RuleSetKind.Unknown, [], []);
        string prompt = _builder.BuildUserPrompt("{}", null, null, 5, null, emptySchema);

        prompt.Should().NotContain("## Input Field Schema");
    }
}
