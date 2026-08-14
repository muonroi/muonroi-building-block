namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Schema extracted from a ruleset JSON for prompt injection.
/// </summary>
public sealed record RuleSetSchema(
    RuleSetKind Kind,
    IReadOnlyList<FieldSchema> InputFields,
    IReadOnlyList<FieldSchema> OutputFields);

/// <summary>
/// A single field in the ruleset schema.
/// </summary>
public sealed record FieldSchema(
    string Name,
    string DataType,
    bool IsRequired,
    string? Description);

/// <summary>
/// Extracts input/output field schema from ruleset JSON for schema-aware prompt injection.
/// Static, no DI required — works on raw JSON only.
/// </summary>
public static class RuleSetSchemaExtractor
{
    private static readonly Regex InputFieldPattern = new(
        @"input\.(\w+(?:\.\w+)*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extract schema from ruleset JSON based on its kind.
    /// </summary>
    public static RuleSetSchema Extract(string ruleSetJson, RuleSetKind kind)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return new RuleSetSchema(kind, [], []);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetJson);
            return kind switch
            {
                RuleSetKind.DecisionTable => ExtractDecisionTable(doc.RootElement, kind),
                RuleSetKind.FlowGraph => ExtractFlowGraph(doc.RootElement, kind),
                RuleSetKind.CodeBased => ExtractCodeBased(doc.RootElement, kind),
                _ => new RuleSetSchema(kind, [], [])
            };
        }
        catch (JsonException)
        {
            return new RuleSetSchema(kind, [], []);
        }
    }

    private static RuleSetSchema ExtractDecisionTable(JsonElement root, RuleSetKind kind)
    {
        List<FieldSchema> inputs = [];
        List<FieldSchema> outputs = [];

        // Parse inputColumns[]
        if (root.TryGetProperty("inputColumns", out JsonElement inputCols)
            && inputCols.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement col in inputCols.EnumerateArray())
            {
                string? name = col.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                string dataType = col.TryGetProperty("dataType", out JsonElement dt)
                    ? dt.GetString() ?? "string"
                    : "string";
                bool isRequired = col.TryGetProperty("isRequired", out JsonElement req)
                    && req.ValueKind == JsonValueKind.True;
                string? description = col.TryGetProperty("description", out JsonElement desc)
                    ? desc.GetString()
                    : null;

                inputs.Add(new FieldSchema(name, dataType, isRequired, description));
            }
        }

        // Parse outputColumns[]
        if (root.TryGetProperty("outputColumns", out JsonElement outputCols)
            && outputCols.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement col in outputCols.EnumerateArray())
            {
                string? name = col.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                string dataType = col.TryGetProperty("dataType", out JsonElement dt)
                    ? dt.GetString() ?? "string"
                    : "string";

                outputs.Add(new FieldSchema(name, dataType, false, null));
            }
        }

        return new RuleSetSchema(kind, inputs, outputs);
    }

    private static RuleSetSchema ExtractFlowGraph(JsonElement root, RuleSetKind kind)
    {
        HashSet<string> inputFieldNames = new(StringComparer.OrdinalIgnoreCase);
        List<FieldSchema> outputs = [];

        // Resolve the flowGraph element (top-level or nested)
        JsonElement graphRoot = root.TryGetProperty("flowGraph", out JsonElement fg) ? fg : root;

        // Parse nodes to extract FEEL expressions and outputFields
        if (graphRoot.TryGetProperty("nodes", out JsonElement nodes)
            && nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement node in nodes.EnumerateArray())
            {
                // Extract from node data
                if (!node.TryGetProperty("data", out JsonElement data)) continue;

                // Extract input fields from FEEL expressions (condition, outputExpression)
                ExtractFieldsFromFeelExpressions(data, inputFieldNames);

                // Extract from outputFields array
                if (data.TryGetProperty("outputFields", out JsonElement outputFields)
                    && outputFields.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement field in outputFields.EnumerateArray())
                    {
                        string? path = field.TryGetProperty("path", out JsonElement p) ? p.GetString() : null;
                        if (string.IsNullOrWhiteSpace(path)) continue;

                        string dataType = field.TryGetProperty("dataType", out JsonElement dt)
                            ? dt.GetString() ?? "string"
                            : "string";

                        outputs.Add(new FieldSchema(path, dataType, false, null));
                    }
                }

                // Extract from triggerData (start node)
                if (data.TryGetProperty("triggerData", out JsonElement triggerData))
                {
                    ExtractFieldsFromTriggerData(triggerData, inputFieldNames);
                }

                // Extract from parameters
                if (data.TryGetProperty("parameters", out JsonElement parameters)
                    && parameters.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty param in parameters.EnumerateObject())
                    {
                        inputFieldNames.Add(param.Name);
                    }
                }
            }
        }

        List<FieldSchema> inputs = [.. inputFieldNames.Select(name => new FieldSchema(name, "unknown", false, null))];

        return new RuleSetSchema(kind, inputs, outputs);
    }

    private static void ExtractFieldsFromFeelExpressions(JsonElement data, HashSet<string> fields)
    {
        // Check condition, outputExpression, expression properties
        string[] expressionProps = ["condition", "outputExpression", "expression", "feelExpression"];

        foreach (string prop in expressionProps)
        {
            if (data.TryGetProperty(prop, out JsonElement expr)
                && expr.ValueKind == JsonValueKind.String)
            {
                string? exprStr = expr.GetString();
                if (string.IsNullOrWhiteSpace(exprStr)) continue;

                foreach (Match match in InputFieldPattern.Matches(exprStr))
                {
                    fields.Add(match.Groups[1].Value);
                }
            }
        }
    }

    private static void ExtractFieldsFromTriggerData(JsonElement triggerData, HashSet<string> fields)
    {
        if (triggerData.TryGetProperty("inputSchema", out JsonElement schema)
            && schema.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in schema.EnumerateObject())
            {
                fields.Add(prop.Name);
            }
        }

        // Also check properties/fields arrays
        string[] arrayProps = ["properties", "fields", "inputs"];
        foreach (string prop in arrayProps)
        {
            if (triggerData.TryGetProperty(prop, out JsonElement arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    string? name = item.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                        fields.Add(name);
                }
            }
        }
    }

    private static RuleSetSchema ExtractCodeBased(JsonElement root, RuleSetKind kind)
    {
        HashSet<string> inputFieldNames = new(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("rules", out JsonElement rules)
            && rules.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement rule in rules.EnumerateArray())
            {
                // Extract from parameters
                if (rule.TryGetProperty("parameters", out JsonElement parameters))
                {
                    if (parameters.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement param in parameters.EnumerateArray())
                        {
                            string? name = param.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(name))
                                inputFieldNames.Add(name);
                        }
                    }
                    else if (parameters.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty param in parameters.EnumerateObject())
                        {
                            inputFieldNames.Add(param.Name);
                        }
                    }
                }

                // Extract from condition expressions
                if (rule.TryGetProperty("condition", out JsonElement condition)
                    && condition.ValueKind == JsonValueKind.String)
                {
                    string? condStr = condition.GetString();
                    if (!string.IsNullOrWhiteSpace(condStr))
                    {
                        foreach (Match match in InputFieldPattern.Matches(condStr))
                        {
                            inputFieldNames.Add(match.Groups[1].Value);
                        }
                    }
                }
            }
        }

        List<FieldSchema> inputs = [.. inputFieldNames.Select(name => new FieldSchema(name, "unknown", false, null))];

        return new RuleSetSchema(kind, inputs, []);
    }
}
