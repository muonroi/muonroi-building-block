using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Full analysis of a flow graph: all enumerated paths and node/edge summaries.
/// </summary>
public sealed record FlowGraphAnalysis(
    IReadOnlyList<FlowPath> Paths,
    IReadOnlyList<FlowNodeSummary> Nodes,
    IReadOnlyList<FlowEdgeSummary> Edges);

/// <summary>
/// A single enumerated path through the flow graph from source to sink node.
/// </summary>
public sealed record FlowPath(
    string PathId,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeTypes,
    string Description);

/// <summary>
/// Summary of a single flow graph node with its I/O field information.
/// </summary>
public sealed record FlowNodeSummary(
    string NodeId,
    string Label,
    string NodeType,
    IReadOnlyList<string> InputFields,
    IReadOnlyList<string> OutputFields);

/// <summary>
/// Summary of a directed edge between two flow graph nodes.
/// </summary>
public sealed record FlowEdgeSummary(
    string Source,
    string Target,
    string EdgeType);

/// <summary>
/// Analyzes flow graph JSON to enumerate all paths from source to sink nodes.
/// Parses JSON directly (RuleFlowGraphModels are internal to the Runtime assembly).
/// </summary>
public static class FlowGraphPathAnalyzer
{
    private const int MaxPaths = 50;

    /// <summary>
    /// Analyzes a ruleset JSON string and returns all enumerated flow paths and node summaries.
    /// Returns empty analysis for invalid or non-flow-graph JSON.
    /// </summary>
    public static FlowGraphAnalysis Analyze(string ruleSetJson)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return new FlowGraphAnalysis([], [], []);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetJson);
            JsonElement root = doc.RootElement;

            // Resolve flowGraph element (top-level or nested under "flowGraph" key)
            JsonElement graphRoot = root.TryGetProperty("flowGraph", out JsonElement fg) ? fg : root;

            if (!graphRoot.TryGetProperty("nodes", out JsonElement nodesElement)
                || nodesElement.ValueKind != JsonValueKind.Array)
                return new FlowGraphAnalysis([], [], []);

            // Parse nodes
            List<FlowNodeSummary> nodeSummaries = [];
            Dictionary<string, string> nodeLabels = new(StringComparer.Ordinal);
            Dictionary<string, string> nodeTypes = new(StringComparer.Ordinal);

            foreach (JsonElement node in nodesElement.EnumerateArray())
            {
                string? id = node.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;

                string label = node.TryGetProperty("label", out JsonElement labelEl)
                    ? labelEl.GetString() ?? id
                    : id;

                string nodeType = node.TryGetProperty("type", out JsonElement typeEl)
                    ? typeEl.GetString() ?? "unknown"
                    : "unknown";

                nodeLabels[id] = label;
                nodeTypes[id] = nodeType;

                // Extract input/output fields from node data
                List<string> inputFields = [];
                List<string> outputFields = [];

                if (node.TryGetProperty("data", out JsonElement data))
                {
                    // Extract input fields from FEEL expressions
                    ExtractInputFieldsFromData(data, inputFields);

                    // Extract output fields
                    if (data.TryGetProperty("outputFields", out JsonElement outFields)
                        && outFields.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement field in outFields.EnumerateArray())
                        {
                            string? path = field.TryGetProperty("path", out JsonElement p) ? p.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(path))
                                outputFields.Add(path);
                        }
                    }
                }

                nodeSummaries.Add(new FlowNodeSummary(id, label, nodeType, inputFields, outputFields));
            }

            // Parse edges
            List<FlowEdgeSummary> edgeSummaries = [];
            // adjacency: source -> list of (target, edgeType)
            Dictionary<string, List<(string Target, string EdgeType)>> adjacency = new(StringComparer.Ordinal);
            // in-degree for finding source nodes
            Dictionary<string, int> inDegree = new(StringComparer.Ordinal);

            // Initialize in-degree for all nodes
            foreach (string nodeId in nodeLabels.Keys)
            {
                inDegree[nodeId] = 0;
            }

            if (graphRoot.TryGetProperty("edges", out JsonElement edgesElement)
                && edgesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement edge in edgesElement.EnumerateArray())
                {
                    string? source = edge.TryGetProperty("source", out JsonElement srcEl) ? srcEl.GetString() : null;
                    string? target = edge.TryGetProperty("target", out JsonElement tgtEl) ? tgtEl.GetString() : null;
                    string edgeType = edge.TryGetProperty("edgeType", out JsonElement etEl)
                        ? etEl.GetString() ?? "always"
                        : "always";

                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;

                    edgeSummaries.Add(new FlowEdgeSummary(source, target, edgeType));

                    if (!adjacency.TryGetValue(source, out List<(string, string)>? neighbors))
                    {
                        neighbors = [];
                        adjacency[source] = neighbors;
                    }
                    neighbors.Add((target, edgeType));

                    inDegree.TryGetValue(target, out int currentDegree);
                    inDegree[target] = currentDegree + 1;
                }
            }

            // Find source nodes (in-degree == 0)
            HashSet<string> sourceNodes = new(
                inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key),
                StringComparer.Ordinal);

            // Find sink nodes (no outgoing edges)
            HashSet<string> sinkNodes = new(
                nodeLabels.Keys.Where(n => !adjacency.ContainsKey(n) || adjacency[n].Count == 0),
                StringComparer.Ordinal);

            // DFS path enumeration from each source node to any sink node
            List<FlowPath> paths = [];
            int pathIndex = 0;

            foreach (string source in sourceNodes)
            {
                if (paths.Count >= MaxPaths) break;

                EnumeratePaths(
                    source,
                    adjacency,
                    sinkNodes,
                    nodeLabels,
                    currentPath: [source],
                    currentEdgeTypes: [],
                    paths,
                    ref pathIndex);
            }

            return new FlowGraphAnalysis(paths, nodeSummaries, edgeSummaries);
        }
        catch (JsonException)
        {
            return new FlowGraphAnalysis([], [], []);
        }
    }

    private static void EnumeratePaths(
        string currentNode,
        Dictionary<string, List<(string Target, string EdgeType)>> adjacency,
        HashSet<string> sinkNodes,
        Dictionary<string, string> nodeLabels,
        List<string> currentPath,
        List<string> currentEdgeTypes,
        List<FlowPath> results,
        ref int pathIndex)
    {
        if (results.Count >= MaxPaths) return;

        // If this is a sink node, record the path
        if (sinkNodes.Contains(currentNode))
        {
            string description = BuildPathDescription(currentPath, currentEdgeTypes, nodeLabels);
            results.Add(new FlowPath(
                PathId: $"path-{++pathIndex}",
                NodeIds: [.. currentPath],
                EdgeTypes: [.. currentEdgeTypes],
                Description: description));
            return;
        }

        if (!adjacency.TryGetValue(currentNode, out List<(string Target, string EdgeType)>? neighbors))
            return;

        foreach ((string target, string edgeType) in neighbors)
        {
            // Prevent cycles — skip if already in path
            if (currentPath.Contains(target)) continue;

            currentPath.Add(target);
            currentEdgeTypes.Add(edgeType);

            EnumeratePaths(target, adjacency, sinkNodes, nodeLabels, currentPath, currentEdgeTypes, results, ref pathIndex);

            currentPath.RemoveAt(currentPath.Count - 1);
            currentEdgeTypes.RemoveAt(currentEdgeTypes.Count - 1);

            if (results.Count >= MaxPaths) return;
        }
    }

    private static string BuildPathDescription(
        List<string> nodeIds,
        List<string> edgeTypes,
        Dictionary<string, string> nodeLabels)
    {
        if (nodeIds.Count == 0) return "(empty path)";

        List<string> parts = [];
        for (int i = 0; i < nodeIds.Count; i++)
        {
            string label = nodeLabels.TryGetValue(nodeIds[i], out string? lbl) ? lbl : nodeIds[i];
            if (i == 0)
            {
                parts.Add(label);
            }
            else
            {
                string edgeType = i - 1 < edgeTypes.Count ? edgeTypes[i - 1] : "always";
                parts.Add($"-[{edgeType}]→ {label}");
            }
        }

        return string.Join(" ", parts);
    }

    private static readonly System.Text.RegularExpressions.Regex InputPattern =
        new(@"input\.(\w+(?:\.\w+)*)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void ExtractInputFieldsFromData(JsonElement data, List<string> inputFields)
    {
        string[] expressionProps = ["condition", "outputExpression", "expression", "feelExpression"];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string prop in expressionProps)
        {
            if (data.TryGetProperty(prop, out JsonElement expr))
            {
                string? body = null;

                if (expr.ValueKind == JsonValueKind.String)
                {
                    body = expr.GetString();
                }
                else if (expr.ValueKind == JsonValueKind.Object
                    && expr.TryGetProperty("body", out JsonElement bodyEl))
                {
                    body = bodyEl.GetString();
                }

                if (string.IsNullOrWhiteSpace(body)) continue;

                foreach (System.Text.RegularExpressions.Match match in InputPattern.Matches(body))
                {
                    string field = match.Groups[1].Value;
                    if (seen.Add(field))
                        inputFields.Add(field);
                }
            }
        }
    }
}
