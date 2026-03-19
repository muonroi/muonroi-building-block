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
        new(["condition", "action", "decision-table", "sub-flow", "liquid", "connector"],
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
        HashSet<string> executableNodeIds = [.. executableNodes.Select(node => node.Id)];

        List<RuleFlowNode> ordered = TopologicalSort(executableNodes, graph.Edges);

        Dictionary<string, List<RuleGraphIncomingEdge>> incomingEdges = BuildIncomingEdges(graph.Edges, executableNodeIds);
        HashSet<string> nodesWithAlwaysEdges = BuildOutgoingBranchSet(graph.Edges, "always");
        HashSet<string> nodesWithOnFalseEdges = BuildOutgoingBranchSet(graph.Edges, "on-false");
        HashSet<string> nodesWithOnErrorEdges = BuildOutgoingBranchSet(graph.Edges, "on-error");

        List<RuleGraphEntry> entries = new(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            RuleFlowNode node = ordered[i];
            entries.Add(MapNodeToEntry(
                node,
                i,
                incomingEdges.TryGetValue(node.Id, out List<RuleGraphIncomingEdge>? incoming) ? incoming : [],
                nodesWithAlwaysEdges.Contains(node.Id),
                nodesWithOnFalseEdges.Contains(node.Id),
                nodesWithOnErrorEdges.Contains(node.Id)));
        }

        return entries;
    }

    private static RuleGraphEntry MapNodeToEntry(
        RuleFlowNode node,
        int fallbackOrder,
        IReadOnlyList<RuleGraphIncomingEdge> incomingEdges,
        bool hasAlwaysEdge,
        bool hasOnFalseEdge,
        bool hasOnErrorEdge)
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

        // Determine expression language: node-level > expression-level > default "feel"
        string expressionLanguage = FirstNonEmpty(
            node.ExpressionLanguage,
            data.Expression?.ExpressionLanguage) ?? "feel";

        // Determine FEEL vs JavaScript expression based on language
        string? feelExpression = null;
        string? jsExpression = null;
        if (data.Expression is not null && !string.IsNullOrWhiteSpace(data.Expression.Body))
        {
            if (string.Equals(expressionLanguage, "javascript", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(data.Expression.Language, "javascript", StringComparison.OrdinalIgnoreCase))
            {
                jsExpression = data.Expression.Body;
                expressionLanguage = "javascript";
            }
            else if (string.Equals(data.Expression.Language, "feel", StringComparison.OrdinalIgnoreCase))
            {
                feelExpression = data.Expression.Body;
            }
            else if (string.Equals(data.Expression.Language, "liquid", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(data.Expression.Language, "scriban", StringComparison.OrdinalIgnoreCase))
            {
                // Liquid/Scriban is handled separately below (LiquidRuleAdapter supports both)
            }
        }

        return new RuleGraphEntry
        {
            NodeId   = node.Id,
            NodeType = node.Type,
            RuleCode = ruleCode,
            Order    = data.Order ?? fallbackOrder,
            DependsOn = data.DependsOn ?? [],

            // Expression language
            ExpressionLanguage = expressionLanguage,

            // Type B-1: FEEL/JavaScript condition
            FeelExpression = feelExpression,
            JavaScriptExpression = jsExpression,
            OutputFields = MapOutputFields(data.OutputFields),

            // Type B-2: Liquid
            LiquidTemplate = (string.Equals(data.Expression?.Language, "liquid", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(data.Expression?.Language, "scriban", StringComparison.OrdinalIgnoreCase))
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

            // Type B-5: Connector
            ConnectorType = data.ConnectorType,
            ConnectorConfig = data.ConnectorConfig,
            CredentialId = data.CredentialId,
            ResilienceOptions = data.ConnectorResilience is not null ? new ConnectorResilienceOptions
            {
                RetryCount = data.ConnectorResilience.RetryCount,
                RetryDelaySeconds = data.ConnectorResilience.RetryDelaySeconds,
                TimeoutSeconds = data.ConnectorResilience.TimeoutSeconds,
                CircuitBreakerThreshold = data.ConnectorResilience.CircuitBreakerThreshold,
            } : null,

            IncomingEdges = incomingEdges,
            HasAlwaysEdge = hasAlwaysEdge,
            HasOnFalseEdge = hasOnFalseEdge,
            HasOnErrorEdge = hasOnErrorEdge
        };
    }

    private static IReadOnlyList<FeelOutputField> MapOutputFields(IEnumerable<RuleFlowFeelOutputField>? source)
    {
        if (source is null)
        {
            return [];
        }

        return [.. source
            .Where(field => !string.IsNullOrWhiteSpace(field.Path) && !string.IsNullOrWhiteSpace(field.ValueExpression))
            .Select(field => new FeelOutputField
            {
                Path = field.Path.Trim(),
                ValueExpression = field.ValueExpression.Trim(),
                DataType = string.IsNullOrWhiteSpace(field.DataType) ? "string" : field.DataType.Trim()
            })];
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

    private static Dictionary<string, List<RuleGraphIncomingEdge>> BuildIncomingEdges(
        IEnumerable<RuleFlowEdge> edges,
        ISet<string> executableNodeIds)
    {
        Dictionary<string, List<RuleGraphIncomingEdge>> incomingEdges = new(StringComparer.OrdinalIgnoreCase);
        foreach (RuleFlowEdge edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target) ||
                !executableNodeIds.Contains(edge.Source) || !executableNodeIds.Contains(edge.Target))
            {
                continue;
            }

            if (!incomingEdges.TryGetValue(edge.Target, out List<RuleGraphIncomingEdge>? incoming))
            {
                incoming = [];
                incomingEdges[edge.Target] = incoming;
            }

            incoming.Add(new RuleGraphIncomingEdge
            {
                SourceNodeId = edge.Source,
                EdgeType = string.IsNullOrWhiteSpace(edge.EdgeType) ? "always" : edge.EdgeType.Trim()
            });
        }

        return incomingEdges;
    }

    private static HashSet<string> BuildOutgoingBranchSet(IEnumerable<RuleFlowEdge> edges, string edgeType)
    {
        return [.. edges
            .Where(edge => string.Equals(edge.EdgeType, edgeType, StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))];
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
