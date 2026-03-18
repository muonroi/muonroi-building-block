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
}
