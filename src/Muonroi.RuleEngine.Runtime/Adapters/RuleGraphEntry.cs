namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Parsed representation of one executable node from a MRuleFlowGraph JSON.
/// Produced by <see cref="RuleGraphParser"/>; consumed by the dispatch logic in
/// <c>RulesEngineService.ResolveRuleEntry&lt;TContext&gt;</c>.
/// </summary>
public sealed class RuleGraphEntry
{
    // Identity
    public string NodeId { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty; // condition|action|decision-table|sub-flow|liquid|end|trigger
    public string? RuleCode { get; init; }                 // non-null for Type A (compiled rule)
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];

    // Type B-1: FEEL condition
    public string? FeelExpression { get; init; }
    public IReadOnlyList<FeelOutputField> OutputFields { get; init; } = [];

    // Type B-2: Liquid action
    public string? LiquidTemplate { get; init; }
    public string LiquidOutputFormat { get; init; } = "text"; // json|text|object
    public string? LiquidOutputKey { get; init; }             // FactBag key for rendered output

    // Type B-3: Decision Table
    public string? DecisionTableId { get; init; }
    public string? DecisionTableCode { get; init; }
    public bool FailOnNoMatch { get; init; } = true;

    // Type B-4: Sub Flow
    public string? SubFlowCode { get; init; }
    public IReadOnlyList<SubFlowInputMapping> InputMappings { get; init; } = [];
    public IReadOnlyList<SubFlowOutputMapping> OutputMappings { get; init; } = [];

    // Graph routing metadata
    public IReadOnlyList<RuleGraphIncomingEdge> IncomingEdges { get; init; } = [];
    /// <summary>
    /// Gets a value indicating whether this node has at least one unconditional outgoing edge.
    /// </summary>
    public bool HasAlwaysEdge { get; init; }
    public bool HasOnFalseEdge { get; init; }
    public bool HasOnErrorEdge { get; init; }
}

public sealed class FeelOutputField
{
    public string Path { get; init; } = string.Empty;
    public string ValueExpression { get; init; } = string.Empty;
    public string DataType { get; init; } = "string";
}

/// <summary>Maps a parent scope path to a child trigger input key.</summary>
public sealed class SubFlowInputMapping
{
    public string SourcePath { get; init; } = string.Empty;  // parent fact/context path
    public string TargetPath { get; init; } = string.Empty;  // child trigger input key
    public string? TransformExpression { get; init; }         // optional FEEL transform
}

/// <summary>Maps a child output fact key to a parent fact key.</summary>
public sealed class SubFlowOutputMapping
{
    public string ChildPath { get; init; } = string.Empty;   // child output fact key
    public string ParentPath { get; init; } = string.Empty;  // parent fact key to write
    public bool ExposeToParent { get; init; } = true;
}

public sealed class RuleGraphIncomingEdge
{
    public string SourceNodeId { get; init; } = string.Empty;
    public string EdgeType { get; init; } = "always";
}
