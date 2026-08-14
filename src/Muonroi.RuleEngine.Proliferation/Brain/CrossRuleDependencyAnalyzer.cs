namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Complete analysis of cross-rule data dependencies and conflicts in a flow graph.
/// </summary>
public sealed record CrossRuleAnalysis(
    IReadOnlyList<FactKeyDependency> Dependencies,
    IReadOnlyList<FactKeyConflict> Conflicts);

/// <summary>
/// A data dependency where one node produces a fact key that another node consumes.
/// </summary>
public sealed record FactKeyDependency(
    string ProducerNodeId,
    string ProducerLabel,
    string ConsumerNodeId,
    string ConsumerLabel,
    string FactKey,
    string ProducerDataType);

/// <summary>
/// A conflict in fact key usage between two nodes.
/// </summary>
public sealed record FactKeyConflict(
    string NodeAId,
    string NodeBId,
    string FactKey,
    string ConflictType);

/// <summary>
/// Analyzes cross-rule dependencies in a flow graph ruleset.
/// Detects producer-consumer relationships for FactBag keys and duplicate-producer conflicts.
/// Parses JSON directly (RuleFlowGraphModels are internal to the Runtime assembly).
/// </summary>
public static class CrossRuleDependencyAnalyzer
{
    /// <summary>
    /// Regex matching input field references: input.field or input.field.subField
    /// Captures the full dotted path (e.g., "user.verified" from "input.user.verified").
    /// </summary>
    private static readonly Regex InputPattern = new(
        @"input\.(\w+(?:\.\w+)*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Analyzes a ruleset JSON string for cross-rule fact key dependencies and conflicts.
    /// Returns empty analysis for invalid or non-flow-graph JSON.
    /// </summary>
    public static CrossRuleAnalysis Analyze(string ruleSetJson)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return new CrossRuleAnalysis([], []);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetJson);
            JsonElement root = doc.RootElement;

            // Resolve flowGraph element
            JsonElement graphRoot = root.TryGetProperty("flowGraph", out JsonElement fg) ? fg : root;

            if (!graphRoot.TryGetProperty("nodes", out JsonElement nodesElement)
                || nodesElement.ValueKind != JsonValueKind.Array)
                return new CrossRuleAnalysis([], []);

            // Step 1: For each node, collect produced fact keys (output paths) and consumed fact keys (input refs)
            // producers: nodeId -> list of (factKey, dataType)
            Dictionary<string, List<(string FactKey, string DataType)>> producers = new(StringComparer.Ordinal);
            // consumers: nodeId -> list of factKey
            Dictionary<string, List<string>> consumers = new(StringComparer.Ordinal);
            // labels: nodeId -> label
            Dictionary<string, string> nodeLabels = new(StringComparer.Ordinal);

            foreach (JsonElement node in nodesElement.EnumerateArray())
            {
                string? id = node.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;

                string label = node.TryGetProperty("label", out JsonElement labelEl)
                    ? labelEl.GetString() ?? id
                    : id;
                nodeLabels[id] = label;

                List<(string, string)> nodeOutputs = [];
                List<string> nodeInputs = [];

                if (node.TryGetProperty("data", out JsonElement data))
                {
                    // Collect output paths (produced fact keys)
                    if (data.TryGetProperty("outputFields", out JsonElement outFields)
                        && outFields.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement field in outFields.EnumerateArray())
                        {
                            string? path = field.TryGetProperty("path", out JsonElement p) ? p.GetString() : null;
                            if (string.IsNullOrWhiteSpace(path)) continue;

                            string dataType = field.TryGetProperty("dataType", out JsonElement dt)
                                ? dt.GetString() ?? "unknown"
                                : "unknown";

                            nodeOutputs.Add((path, dataType));
                        }
                    }

                    // Collect input references from FEEL expressions
                    ExtractInputRefs(data, nodeInputs);
                }

                if (nodeOutputs.Count > 0)
                    producers[id] = nodeOutputs;

                if (nodeInputs.Count > 0)
                    consumers[id] = nodeInputs;
            }

            // Step 2: Match producer outputs against consumer inputs
            List<FactKeyDependency> dependencies = [];

            foreach (KeyValuePair<string, List<(string FactKey, string DataType)>> producer in producers)
            {
                string producerNodeId = producer.Key;
                string producerLabel = nodeLabels.TryGetValue(producerNodeId, out string? pl) ? pl : producerNodeId;

                foreach ((string factKey, string dataType) in producer.Value)
                {
                    foreach (KeyValuePair<string, List<string>> consumer in consumers)
                    {
                        if (consumer.Key == producerNodeId) continue; // skip self-references

                        string consumerNodeId = consumer.Key;
                        string consumerLabel = nodeLabels.TryGetValue(consumerNodeId, out string? cl) ? cl : consumerNodeId;

                        // Check if any consumer input matches this fact key
                        // A consumer "input.user.verified" matches producer output "user.verified"
                        bool isConsumed = consumer.Value.Any(inputKey =>
                            string.Equals(inputKey, factKey, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(inputKey, factKey.TrimStart('.'), StringComparison.OrdinalIgnoreCase));

                        if (isConsumed)
                        {
                            dependencies.Add(new FactKeyDependency(
                                ProducerNodeId: producerNodeId,
                                ProducerLabel: producerLabel,
                                ConsumerNodeId: consumerNodeId,
                                ConsumerLabel: consumerLabel,
                                FactKey: factKey,
                                ProducerDataType: dataType));
                        }
                    }
                }
            }

            // Step 3: Detect conflicts — multiple nodes producing the same fact key (duplicate-producer)
            List<FactKeyConflict> conflicts = [];

            // Build: factKey -> list of (nodeId, label) that produce it
            Dictionary<string, List<(string NodeId, string Label)>> factKeyProducers =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<(string FactKey, string DataType)>> producer in producers)
            {
                string nodeId = producer.Key;
                string label = nodeLabels.TryGetValue(nodeId, out string? lbl) ? lbl : nodeId;

                foreach ((string factKey, _) in producer.Value)
                {
                    if (!factKeyProducers.TryGetValue(factKey, out List<(string, string)>? list))
                    {
                        list = [];
                        factKeyProducers[factKey] = list;
                    }
                    list.Add((nodeId, label));
                }
            }

            // Report duplicate producers as conflicts
            HashSet<string> reportedConflicts = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<(string NodeId, string Label)>> kv in factKeyProducers)
            {
                if (kv.Value.Count < 2) continue;

                // Report each pair (without duplicates)
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    for (int j = i + 1; j < kv.Value.Count; j++)
                    {
                        string conflictKey = $"{kv.Key}:{kv.Value[i].NodeId}:{kv.Value[j].NodeId}";
                        if (reportedConflicts.Add(conflictKey))
                        {
                            conflicts.Add(new FactKeyConflict(
                                NodeAId: kv.Value[i].NodeId,
                                NodeBId: kv.Value[j].NodeId,
                                FactKey: kv.Key,
                                ConflictType: "duplicate-producer"));
                        }
                    }
                }
            }

            return new CrossRuleAnalysis(dependencies, conflicts);
        }
        catch (JsonException)
        {
            return new CrossRuleAnalysis([], []);
        }
    }

    private static void ExtractInputRefs(JsonElement data, List<string> inputRefs)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        string[] expressionProps = ["condition", "outputExpression", "expression", "feelExpression"];

        foreach (string prop in expressionProps)
        {
            if (!data.TryGetProperty(prop, out JsonElement exprEl)) continue;

            string? body = null;

            if (exprEl.ValueKind == JsonValueKind.String)
            {
                body = exprEl.GetString();
            }
            else if (exprEl.ValueKind == JsonValueKind.Object
                && exprEl.TryGetProperty("body", out JsonElement bodyEl))
            {
                body = bodyEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(body)) continue;

            foreach (Match match in InputPattern.Matches(body))
            {
                string field = match.Groups[1].Value;
                if (seen.Add(field))
                    inputRefs.Add(field);
            }
        }

        // Also check outputFields valueExpression
        if (data.TryGetProperty("outputFields", out JsonElement outFields)
            && outFields.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement field in outFields.EnumerateArray())
            {
                string? valueExpr = field.TryGetProperty("valueExpression", out JsonElement ve)
                    ? ve.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(valueExpr)) continue;

                foreach (Match match in InputPattern.Matches(valueExpr))
                {
                    string fieldName = match.Groups[1].Value;
                    if (seen.Add(fieldName))
                        inputRefs.Add(fieldName);
                }
            }
        }
    }
}
