using System.Text;
using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Default prompt builder with JSON schema definition, few-shot examples,
/// and chain-of-thought hints for high-quality scenario generation.
/// </summary>
public sealed class DefaultPromptBuilder : IPromptBuilder
{
    public string BuildSystemPrompt() => SystemPrompt;

    public string BuildUserPrompt(
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        int budget,
        IReadOnlyList<string>? focusAreas)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Generate {budget} test scenarios.");
        sb.AppendLine();
        sb.AppendLine("Rule definition:");
        sb.AppendLine(ruleSetJson);

        if (executionResult.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine("Previous execution result:");
            sb.AppendLine(executionResult.Value.ToString());
        }

        if (factBagSnapshot.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine("Previous FactBag state:");
            sb.AppendLine(factBagSnapshot.Value.ToString());
        }

        if (focusAreas is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"Focus areas: {string.Join(", ", focusAreas)}");
        }

        return sb.ToString();
    }

    private const string SystemPrompt = """
        You are a rule proliferation analyzer for a business rule engine.
        Given a business rule definition (flow graph JSON) and optionally its previous execution result,
        generate test scenarios that cover:
        1. Business edge cases: boundary values, null inputs, rare combinations, invalid states
        2. Technical stress cases: concurrent execution, timeout scenarios, error recovery paths

        Think step-by-step: first identify the rule's input fields and types, then consider boundary values
        and edge cases for each field, then generate scenarios that combine multiple edge conditions.

        Each scenario MUST follow this exact JSON schema:
        {
          "scenario": "string — descriptive scenario name",
          "type": "string — either 'business' or 'technical'",
          "reason": "string — why this case matters (1 sentence)",
          "inputFacts": { "key": "value pairs matching the rule's expected input" },
          "expectedBehavior": "string — what should happen"
        }

        ## Example 1 — Business edge case
        [
          {
            "scenario": "Order amount at zero boundary",
            "type": "business",
            "reason": "Zero is a common boundary that may bypass minimum-amount checks",
            "inputFacts": {"orderAmount": 0, "currency": "USD", "customerId": "C001"},
            "expectedBehavior": "should fail with minimum amount validation error"
          },
          {
            "scenario": "Missing required customer field",
            "type": "business",
            "reason": "Null customer ID tests required-field validation",
            "inputFacts": {"orderAmount": 100, "currency": "USD", "customerId": null},
            "expectedBehavior": "should fail with missing customer ID error"
          }
        ]

        ## Example 2 — Technical stress case
        [
          {
            "scenario": "Extremely large numeric input",
            "type": "technical",
            "reason": "Tests numeric overflow handling in rule evaluation",
            "inputFacts": {"amount": 999999999999, "currency": "USD"},
            "expectedBehavior": "should handle gracefully without overflow exception"
          }
        ]

        Respond ONLY with a valid JSON array of scenarios. No markdown, no explanation, no wrapping.
        """;
}
