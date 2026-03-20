using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Coverage report for a ruleset's input fields across generated scenarios.
/// </summary>
public sealed record CoverageReport(
    IReadOnlyList<string> CoveredFields,
    IReadOnlyList<string> UncoveredFields,
    double CoveragePercentage);

/// <summary>
/// Coverage report for flow graph nodes and edges.
/// </summary>
public sealed record FlowCoverageReport(
    IReadOnlyList<NodeCoverage> Nodes,
    IReadOnlyList<EdgeCoverage> Edges,
    double NodeCoveragePercentage,
    double EdgeCoveragePercentage);

/// <summary>Coverage status for a single flow node.</summary>
public sealed record NodeCoverage
{
    /// <summary>Identifier of the node.</summary>
    public string NodeId { get; init; } = string.Empty;
    /// <summary>Display label for the node.</summary>
    public string Label { get; init; } = string.Empty;
    /// <summary>Indicates whether the node was exercised.</summary>
    public bool IsCovered { get; init; }

    /// <summary>
    /// Creates a node coverage record.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="label">Node label.</param>
    /// <param name="isCovered">Whether the node was covered.</param>
    public NodeCoverage(string nodeId, string label, bool isCovered)
    {
        NodeId = nodeId;
        Label = label;
        IsCovered = isCovered;
    }

    /// <summary>
    /// Initializes a node coverage record for object-initializer use.
    /// </summary>
    public NodeCoverage()
    {
    }
}

/// <summary>Coverage status for a single flow edge.</summary>
public sealed record EdgeCoverage
{
    /// <summary>Identifier of the edge.</summary>
    public string EdgeId { get; init; } = string.Empty;
    /// <summary>Source node identifier.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>Target node identifier.</summary>
    public string Target { get; init; } = string.Empty;
    /// <summary>Type of the edge.</summary>
    public string EdgeType { get; init; } = string.Empty;
    /// <summary>Indicates whether the edge was exercised.</summary>
    public bool IsCovered { get; init; }

    /// <summary>
    /// Creates an edge coverage record.
    /// </summary>
    /// <param name="edgeId">Edge identifier.</param>
    /// <param name="source">Source node identifier.</param>
    /// <param name="target">Target node identifier.</param>
    /// <param name="edgeType">Edge type.</param>
    /// <param name="isCovered">Whether the edge was covered.</param>
    public EdgeCoverage(string edgeId, string source, string target, string edgeType, bool isCovered)
    {
        EdgeId = edgeId;
        Source = source;
        Target = target;
        EdgeType = edgeType;
        IsCovered = isCovered;
    }

    /// <summary>
    /// Initializes an edge coverage record for object-initializer use.
    /// </summary>
    public EdgeCoverage()
    {
    }
}

/// <summary>
/// Tracks which input fields have been covered by generated scenarios.
/// </summary>
public interface ICoverageTracker
{
    /// <summary>Calculates field coverage for a ruleset.</summary>
    CoverageReport GetCoverage(
        string seedRuleCode,
        IReadOnlyList<NeuronScenario> scenarios,
        RuleSetSchema schema);

    /// <summary>Calculates node and edge coverage for a flow graph.</summary>
    FlowCoverageReport GetFlowCoverage(
        string seedRuleCode,
        IReadOnlyList<NeuronScenario> scenarios,
        string ruleSetJson);
}

/// <summary>
/// Analyzes existing scenarios' InputFacts keys vs schema's InputFields
/// to determine coverage. Also tracks flow graph node/edge coverage.
/// </summary>
public sealed class DefaultCoverageTracker : ICoverageTracker
{
    /// <summary>Calculates field coverage for a ruleset.</summary>
    public CoverageReport GetCoverage(
        string seedRuleCode,
        IReadOnlyList<NeuronScenario> scenarios,
        RuleSetSchema schema)
    {
        if (schema.InputFields.Count == 0)
            return new CoverageReport([], [], 0);

        HashSet<string> testedFields = new(StringComparer.OrdinalIgnoreCase);

        foreach (NeuronScenario scenario in scenarios)
        {
            if (scenario.InputFacts.ValueKind != JsonValueKind.Object) continue;

            foreach (JsonProperty prop in scenario.InputFacts.EnumerateObject())
            {
                testedFields.Add(prop.Name);
            }
        }

        List<string> covered = [];
        List<string> uncovered = [];

        foreach (FieldSchema field in schema.InputFields)
        {
            if (testedFields.Contains(field.Name))
                covered.Add(field.Name);
            else
                uncovered.Add(field.Name);
        }

        double percentage = schema.InputFields.Count > 0
            ? (double)covered.Count / schema.InputFields.Count * 100
            : 0;

        return new CoverageReport(covered, uncovered, Math.Round(percentage, 1));
    }

    /// <summary>Calculates node and edge coverage for a flow graph.</summary>
    public FlowCoverageReport GetFlowCoverage(
        string seedRuleCode,
        IReadOnlyList<NeuronScenario> scenarios,
        string ruleSetJson)
    {
        if (string.IsNullOrWhiteSpace(ruleSetJson))
            return new FlowCoverageReport([], [], 0, 0);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetJson);
            JsonElement root = doc.RootElement;

            // Resolve flowGraph element
            JsonElement graphRoot = root.TryGetProperty("flowGraph", out JsonElement fg) ? fg : root;

            if (!graphRoot.TryGetProperty("nodes", out JsonElement nodesEl)
                || nodesEl.ValueKind != JsonValueKind.Array)
            {
                return new FlowCoverageReport([], [], 0, 0);
            }

            // Collect all node IDs and labels
            List<(string id, string label)> allNodes = [];
            foreach (JsonElement node in nodesEl.EnumerateArray())
            {
                string? id = node.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                if (id is null) continue;
                string label = node.TryGetProperty("label", out JsonElement labelEl)
                    ? labelEl.GetString() ?? id
                    : id;
                allNodes.Add((id, label));
            }

            // Collect all edges
            List<(string id, string source, string target, string edgeType)> allEdges = [];
            if (graphRoot.TryGetProperty("edges", out JsonElement edgesEl)
                && edgesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement edge in edgesEl.EnumerateArray())
                {
                    string? eid = edge.TryGetProperty("id", out JsonElement eidEl) ? eidEl.GetString() : null;
                    string? source = edge.TryGetProperty("source", out JsonElement srcEl) ? srcEl.GetString() : null;
                    string? target = edge.TryGetProperty("target", out JsonElement tgtEl) ? tgtEl.GetString() : null;
                    string edgeType = edge.TryGetProperty("edgeType", out JsonElement etEl)
                        ? etEl.GetString() ?? "always"
                        : "always";
                    if (eid is null || source is null || target is null) continue;
                    allEdges.Add((eid, source, target, edgeType));
                }
            }

            // Check which nodes were executed by scanning scenario results' OutputFacts
            // FactBag keys: __graph.node.{nodeId}.executed = true
            HashSet<string> executedNodes = new(StringComparer.OrdinalIgnoreCase);
            foreach (NeuronScenario scenario in scenarios)
            {
                // Check GeneratedRuleFlowGraph or InputFacts for graph execution data
                ScanForExecutedNodes(scenario.InputFacts, executedNodes);
            }

            // Build node coverage
            List<NodeCoverage> nodeCoverage = [.. allNodes
                .Select(n => new NodeCoverage
                {
                    NodeId = n.id,
                    Label = n.label,
                    IsCovered = executedNodes.Contains(n.id)
                })];

            // Build edge coverage: edge is covered if both source and target are covered
            List<EdgeCoverage> edgeCoverage = [.. allEdges
                .Select(e => new EdgeCoverage
                {
                    EdgeId = e.id,
                    Source = e.source,
                    Target = e.target,
                    EdgeType = e.edgeType,
                    IsCovered = executedNodes.Contains(e.source) && executedNodes.Contains(e.target)
                })];

            double nodePct = allNodes.Count > 0
                ? Math.Round((double)nodeCoverage.Count(n => n.IsCovered) / allNodes.Count * 100, 1)
                : 0;
            double edgePct = allEdges.Count > 0
                ? Math.Round((double)edgeCoverage.Count(e => e.IsCovered) / allEdges.Count * 100, 1)
                : 0;

            return new FlowCoverageReport(nodeCoverage, edgeCoverage, nodePct, edgePct);
        }
        catch (JsonException)
        {
            return new FlowCoverageReport([], [], 0, 0);
        }
    }

    private static void ScanForExecutedNodes(JsonElement facts, HashSet<string> executedNodes)
    {
        if (facts.ValueKind != JsonValueKind.Object) return;

        foreach (JsonProperty prop in facts.EnumerateObject())
        {
            // Match keys like __graph.node.{nodeId}.executed
            if (prop.Name.StartsWith("__graph.node.", StringComparison.Ordinal)
                && prop.Name.EndsWith(".executed", StringComparison.Ordinal))
            {
                string nodeId = prop.Name["__graph.node.".Length..^".executed".Length];
                if (!string.IsNullOrWhiteSpace(nodeId))
                    executedNodes.Add(nodeId);
            }
        }
    }
}
