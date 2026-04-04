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
    /// <summary>Builds the generic system prompt.</summary>
    public string BuildSystemPrompt() => GenericSystemPrompt;

    /// <summary>Builds the system prompt for a specific ruleset kind.</summary>
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

    /// <summary>Builds the user prompt for proliferation analysis.</summary>
    public string BuildUserPrompt(
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        int budget,
        IReadOnlyList<string>? focusAreas,
        RuleSetSchema? schema = null,
        FlowGraphAnalysis? flowAnalysis = null,
        CrossRuleAnalysis? crossRuleAnalysis = null,
        BoundaryExtractionResult? boundaries = null,
        HitPolicyAnalysis? hitPolicyAnalysis = null)
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

        // Inject flow graph path analysis for workflow-level scope
        if (flowAnalysis is { Paths.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Flow Graph Paths");
            sb.AppendLine("The workflow has the following execution paths (source → sink):");
            foreach (FlowPath path in flowAnalysis.Paths)
            {
                sb.AppendLine($"- [{path.PathId}] {path.Description}");
            }
            sb.AppendLine();
            sb.AppendLine("Focus on generating scenarios that exercise DIFFERENT paths, especially on-false and on-error edges.");
        }

        if (flowAnalysis is { Nodes.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Flow Graph Nodes");
            sb.AppendLine("Node details with input/output fields:");
            foreach (FlowNodeSummary node in flowAnalysis.Nodes)
            {
                string inputs = node.InputFields.Count > 0 ? string.Join(", ", node.InputFields) : "(none)";
                string outputs = node.OutputFields.Count > 0 ? string.Join(", ", node.OutputFields) : "(none)";
                sb.AppendLine($"- [{node.NodeType}] {node.Label}: inputs=[{inputs}], outputs=[{outputs}]");
            }
        }

        // Inject cross-rule dependency analysis
        if (crossRuleAnalysis is { Dependencies.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Cross-Rule Dependencies");
            sb.AppendLine("These fact keys flow between nodes (producer → consumer):");
            foreach (FactKeyDependency dep in crossRuleAnalysis.Dependencies)
            {
                sb.AppendLine($"- '{dep.FactKey}' ({dep.ProducerDataType}): produced by [{dep.ProducerLabel}], consumed by [{dep.ConsumerLabel}]");
            }
            sb.AppendLine();
            sb.AppendLine("Generate scenarios that test these cross-rule data flows — e.g., when the producer outputs an unexpected value, does the consumer handle it correctly?");
        }

        if (crossRuleAnalysis is { Conflicts.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Fact Key Conflicts");
            sb.AppendLine("Warning: these fact keys have conflicts that may cause ordering issues:");
            foreach (FactKeyConflict conflict in crossRuleAnalysis.Conflicts)
            {
                sb.AppendLine($"- '{conflict.FactKey}': {conflict.ConflictType} between [{conflict.NodeAId}] and [{conflict.NodeBId}]");
            }
        }

        // Inject FEEL boundary values for type-aware scenario generation
        if (boundaries is { Boundaries.Count: > 0 } || boundaries is { Branches.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Boundary Values");
            sb.AppendLine("Generate scenarios that exercise these extracted boundary test values:");

            if (boundaries.Boundaries.Count > 0)
            {
                foreach (FieldBoundary b in boundaries.Boundaries)
                {
                    string vals = string.Join(", ", b.BoundaryValues);
                    sb.AppendLine($"- {b.Field} ({b.Operator} {b.ThresholdValue}): test with [{vals}]");
                }
            }

            if (boundaries.Branches.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Branch paths to cover:");
                foreach (BranchHint branch in boundaries.Branches)
                {
                    sb.AppendLine($"- {branch.Field}: {branch.TruePath}");
                    sb.AppendLine($"  {branch.Field}: {branch.FalsePath}");
                }
            }
        }

        // Inject decision table hit-policy analysis
        if (hitPolicyAnalysis is { PolicyHints.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Hit Policy Analysis");
            sb.AppendLine($"Hit policy: {hitPolicyAnalysis.HitPolicy} | Rows: {hitPolicyAnalysis.RowCount}");
            if (hitPolicyAnalysis.InputColumns.Count > 0)
                sb.AppendLine($"Input columns: {string.Join(", ", hitPolicyAnalysis.InputColumns)}");
            sb.AppendLine("Policy-specific hints:");
            foreach (string hint in hitPolicyAnalysis.PolicyHints)
                sb.AppendLine($"- {hint}");
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

    /// <summary>
    /// Builds the user prompt for natural language to ruleset conversion.
    /// </summary>
    /// <param name="description">Plain-text business rule description from the user.</param>
    /// <param name="outputFormat">"flowgraph" or "decisiontable"</param>
    /// <summary>Builds the user prompt for natural language conversion.</summary>
    public string BuildNlConversionPrompt(string description, string outputFormat)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Convert the following business rule description to a {outputFormat} JSON definition:");
        sb.AppendLine();
        sb.AppendLine($"Description: {description}");
        sb.AppendLine();

        if (string.Equals(outputFormat, "decisiontable", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("Output a JSON object with this structure:");
            sb.AppendLine("""
                {
                  "inputColumns": [{ "name": "fieldName", "dataType": "number|string|boolean", "isRequired": true }],
                  "outputColumns": [{ "name": "resultField", "dataType": "number|string|boolean" }],
                  "rules": [{ "conditions": ["condition expression"], "actions": ["output value"] }]
                }
                """);
        }
        else
        {
            sb.AppendLine("Output a JSON object with this structure:");
            sb.AppendLine("""
                {
                  "nodes": [
                    { "id": "start", "type": "Start", "data": { "triggerData": { "inputSchema": { "fieldName": "dataType" } } } },
                    { "id": "rule1", "type": "RuleTask", "data": { "condition": "input.fieldName > value", "outputFields": [{ "path": "outputField", "dataType": "number" }] } },
                    { "id": "end", "type": "End", "data": {} }
                  ],
                  "edges": [
                    { "source": "start", "target": "rule1", "type": "always" },
                    { "source": "rule1", "target": "end", "type": "always" }
                  ]
                }
                """);
        }

        sb.AppendLine();
        sb.AppendLine("Use field names derived from the description (camelCase, no spaces). Output ONLY the JSON, nothing else.");
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
