using System.Text;
using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Default prompt builder with JSON schema definition, few-shot examples,
/// and chain-of-thought hints for high-quality scenario generation.
/// Supports rule-type-specific prompt sections.
/// </summary>
public sealed class DefaultPromptBuilder : IPromptBuilder
{
    public string BuildSystemPrompt() => GenericSystemPrompt;

    public string BuildSystemPrompt(RuleSetKind kind)
    {
        string kindSection = kind switch
        {
            RuleSetKind.FlowGraph => FlowGraphSection,
            RuleSetKind.DecisionTable => DecisionTableSection,
            RuleSetKind.CodeBased => CodeBasedSection,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(kindSection))
            return GenericSystemPrompt;

        return $"{GenericSystemPrompt}\n\n{kindSection}";
    }

    public string BuildUserPrompt(
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        int budget,
        IReadOnlyList<string>? focusAreas,
        RuleSetSchema? schema = null)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Generate {budget} test scenarios.");

        // Inject schema section FIRST so AI knows field names before reading the rule definition
        if (schema is { InputFields.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Input Field Schema");
            sb.AppendLine("The rule expects these exact input fields:");
            foreach (FieldSchema field in schema.InputFields)
            {
                string required = field.IsRequired ? ", required" : "";
                string desc = field.Description is not null ? $" — {field.Description}" : "";
                sb.AppendLine($"- {field.Name} ({field.DataType}{required}){desc}");
            }
            sb.AppendLine();
            sb.AppendLine("You MUST use exactly these field names in inputFacts. Do NOT invent field names.");
        }

        if (schema is { OutputFields.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Output Fields");
            sb.AppendLine("The rule produces these output fields:");
            foreach (FieldSchema field in schema.OutputFields)
            {
                sb.AppendLine($"- {field.Name} ({field.DataType})");
            }
        }

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

    private const string GenericSystemPrompt = """
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

    internal const string FlowGraphSection = """
        ## Rule Type: Flow Graph
        This ruleset is a flow graph with nodes and edges.
        Additional focus areas for scenario generation:
        - PATH COVERAGE: Test each edge type (always, on-true, on-false, on-error) to ensure all paths are exercised.
        - UNREACHABLE NODES: Consider inputs that should reach every node — identify nodes that may never be reached.
        - ERROR EDGES: Test inputs that trigger on-error edges to verify error recovery paths.
        - GATEWAY LOGIC: For exclusive gateways, test inputs at the exact boundary where the gateway switches paths.
        - CYCLE DETECTION: If the graph has loops, test inputs that could cause excessive iterations.
        """;

    internal const string DecisionTableSection = """
        ## Rule Type: Decision Table
        This ruleset is a decision table with input columns, output columns, and a hit policy.
        Additional focus areas for scenario generation:
        - ROW COVERAGE: Generate at least one scenario per row to ensure every rule fires.
        - BOUNDARY VALUES: For each input column, test values at the exact boundary (e.g., if a column checks "> 100", test 100 and 101).
        - GAP ANALYSIS: Test input combinations that match NO rows — these are gaps in the decision table.
        - HIT POLICY: Consider the hit policy (First, Unique, Collect, Priority) and test scenarios where multiple rows match.
        - COLUMN TYPES: Test null/empty values for each input column to verify handling of missing data.
        """;

    internal const string CodeBasedSection = """
        ## Rule Type: Code-Based Rules
        These are code-first rules with explicit boolean conditions and execution logic.
        Additional focus areas for scenario generation:
        - BOOLEAN BOUNDARIES: For each condition, test the exact true/false boundary.
        - NULL INPUTS: Test what happens when each expected input field is null or missing.
        - TYPE COERCION: Test inputs with unexpected types (string where number expected, etc.).
        - RULE ORDERING: If rules depend on each other, test scenarios where earlier rules modify the FactBag before later rules execute.
        - EXECUTION MODE: Consider AllOrNothing vs BestEffort behavior — test scenarios that fail mid-pipeline.
        """;
}
