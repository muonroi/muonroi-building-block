using System.Text.Json;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Parses a serialized MRuleFlowGraph JSON string (produced by Rule Studio) into
/// an ordered list of <see cref="RuleGraphEntry"/> using Kahn's topological sort.
/// Trigger and End nodes are excluded from the result.
/// </summary>
public sealed class RuleGraphParser(IMJsonSerializeService json)
{
    private static readonly HashSet<string> ExecutableTypes =
        new(["condition", "action", "decision-table", "sub-flow", "liquid"],
            StringComparer.OrdinalIgnoreCase);

    public bool CanParse(string graphJson)
    {
        return TryExtractGraph(graphJson, out _);
    }

    public IReadOnlyList<RuleGraphEntry> Parse(string graphJson)
    {
        if (!TryExtractGraph(graphJson, out RuleFlowGraph? graph) || graph is null)
        {
            throw new InvalidOperationException("Invalid or empty graph JSON.");
        }

        List<RuleFlowNode> executableNodes = graph.Nodes
            .Where(n => ExecutableTypes.Contains(n.Type))
            .ToList();

        List<RuleFlowNode> ordered = TopologicalSort(executableNodes, graph.Edges);

        List<RuleGraphEntry> entries = new(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            entries.Add(MapNodeToEntry(ordered[i], i));
        }

        return entries;
    }

    private static RuleGraphEntry MapNodeToEntry(RuleFlowNode node, int fallbackOrder)
    {
        RuleFlowNodeData data = node.Data;
        string? ruleCode = FirstNonEmpty(
            node.RuleCode,
            data.RuleCode,
            string.Equals(data.ContractRef?.SourceType, "rule", StringComparison.OrdinalIgnoreCase)
                ? data.ContractRef?.SourceCode
                : null);
        string? decisionTableId = FirstNonEmpty(
            data.DecisionTableId,
            string.Equals(data.ContractRef?.SourceType, "decision-table", StringComparison.OrdinalIgnoreCase)
                ? data.ContractRef?.SourceCode
                : null);
        string? subFlowCode = FirstNonEmpty(
            data.SubFlowConfig?.TargetFlowCode,
            string.Equals(data.ContractRef?.SourceType, "flow", StringComparison.OrdinalIgnoreCase)
                ? data.ContractRef?.SourceCode
                : null);

        return new RuleGraphEntry
        {
            NodeId   = node.Id,
            NodeType = node.Type,
            RuleCode = ruleCode,
            Order    = data.Order ?? fallbackOrder,
            DependsOn = data.DependsOn ?? [],

            // Type B-1: FEEL
            FeelExpression = string.Equals(data.Expression?.Language, "feel", StringComparison.OrdinalIgnoreCase)
                ? data.Expression!.Body : null,

            // Type B-2: Liquid
            LiquidTemplate = string.Equals(data.Expression?.Language, "liquid", StringComparison.OrdinalIgnoreCase)
                ? data.Expression!.Body : null,
            LiquidOutputFormat = data.LiquidConfig?.OutputFormat ?? "text",
            LiquidOutputKey    = data.LiquidConfig?.OutputFactKey,

            // Type B-3: Decision Table
            DecisionTableId   = decisionTableId,
            DecisionTableCode = FirstNonEmpty(data.DecisionTableCode, decisionTableId),
            FailOnNoMatch     = data.FailOnNoMatch ?? true,

            // Type B-4: Sub Flow
            SubFlowCode    = subFlowCode,
            InputMappings  = MapInputMappings(data.SubFlowConfig?.InputMappings),
            OutputMappings = MapOutputMappings(data.SubFlowConfig?.OutputMappings),
        };
    }

    private static IReadOnlyList<SubFlowInputMapping> MapInputMappings(
        IEnumerable<RuleFlowInputMapping>? source)
    {
        if (source is null) return [];
        return [.. source.Select(m => new SubFlowInputMapping
        {
            SourcePath          = m.SourcePath,
            TargetPath          = m.TargetPath,
            TransformExpression = m.TransformExpression,
        })];
    }

    private static IReadOnlyList<SubFlowOutputMapping> MapOutputMappings(
        IEnumerable<RuleFlowOutputMapping>? source)
    {
        if (source is null) return [];
        return [.. source.Select(m => new SubFlowOutputMapping
        {
            ChildPath      = FirstNonEmpty(m.ChildPath, m.SourcePath) ?? string.Empty,
            ParentPath     = FirstNonEmpty(m.ParentPath, m.TargetPath) ?? string.Empty,
            ExposeToParent = m.ExposeToParent,
        })];
    }

    private bool TryExtractGraph(string graphJson, out RuleFlowGraph? graph)
    {
        graph = json.Deserialize<RuleFlowGraph>(graphJson);
        if (graph is { Nodes.Count: > 0 })
        {
            return true;
        }

        RuleFlowRuleSetEnvelope? envelope = json.Deserialize<RuleFlowRuleSetEnvelope>(graphJson);
        if (envelope?.FlowGraph is { Nodes.Count: > 0 } flowGraph)
        {
            graph = flowGraph;
            return true;
        }

        graph = null;
        return false;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    /// <summary>
    /// Kahn's algorithm topological sort using graph edges.
    /// Nodes not reachable from any edge source start at in-degree 0.
    /// </summary>
    private static List<RuleFlowNode> TopologicalSort(
        List<RuleFlowNode> nodes,
        IEnumerable<RuleFlowEdge> edges)
    {
        Dictionary<string, int> inDegree = nodes.ToDictionary(n => n.Id, _ => 0);
        Dictionary<string, List<string>> adj = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (RuleFlowEdge edge in edges)
        {
            if (adj.ContainsKey(edge.Source) && inDegree.ContainsKey(edge.Target))
            {
                adj[edge.Source].Add(edge.Target);
                inDegree[edge.Target]++;
            }
        }

        Queue<string> queue = new(
            inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        Dictionary<string, RuleFlowNode> nodeMap = nodes.ToDictionary(n => n.Id);
        List<RuleFlowNode> result = new(nodes.Count);

        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (nodeMap.TryGetValue(id, out RuleFlowNode? node))
            {
                result.Add(node);
            }

            if (adj.TryGetValue(id, out List<string>? neighbors))
            {
                foreach (string next in neighbors)
                {
                    if (inDegree.ContainsKey(next) && --inDegree[next] == 0)
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        // Append any nodes not reached by topological sort (isolated nodes)
        HashSet<string> visited = new(result.Select(n => n.Id));
        foreach (RuleFlowNode n in nodes)
        {
            if (!visited.Contains(n.Id))
            {
                result.Add(n);
            }
        }

        return result;
    }
}
