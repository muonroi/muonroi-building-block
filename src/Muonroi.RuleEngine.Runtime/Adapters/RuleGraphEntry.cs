namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Parsed representation of one executable node from a MRuleFlowGraph JSON.
/// Produced by <see cref="RuleGraphParser"/>; consumed by the dispatch logic in
/// <c>RulesEngineService.ResolveRuleEntry&lt;TContext&gt;</c>.
/// </summary>
public sealed class RuleGraphEntry
{
    // Identity
    /// <summary>Gets the node identifier.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Gets the node type.</summary>
    public string NodeType { get; init; } = string.Empty; // condition|action|decision-table|sub-flow|liquid|end|trigger

    /// <summary>Gets the compiled rule code, when applicable.</summary>
    public string? RuleCode { get; init; }                 // non-null for Type A (compiled rule)

    /// <summary>Gets the execution order for this node.</summary>
    public int Order { get; init; }

    /// <summary>Gets the dependency rule codes.</summary>
    public string[] DependsOn { get; init; } = [];

    // Expression language: "feel" (default), "javascript"
    /// <summary>Gets the expression language for this node.</summary>
    public string ExpressionLanguage { get; init; } = "feel";

    // Type B-1: FEEL/JavaScript condition
    /// <summary>Gets the FEEL expression, when configured.</summary>
    public string? FeelExpression { get; init; }

    /// <summary>Gets the JavaScript expression, when configured.</summary>
    public string? JavaScriptExpression { get; init; }

    /// <summary>Gets the output field mappings.</summary>
    public IReadOnlyList<FeelOutputField> OutputFields { get; init; } = [];

    // Type B-2: Liquid action
    /// <summary>Gets the Liquid/Scriban template body.</summary>
    public string? LiquidTemplate { get; init; }

    /// <summary>Gets the Liquid output format.</summary>
    public string LiquidOutputFormat { get; init; } = "text"; // json|text|object

    /// <summary>Gets the Liquid output key.</summary>
    public string? LiquidOutputKey { get; init; }             // FactBag key for rendered output

    // Type B-3: Decision Table
    /// <summary>Gets the decision table identifier.</summary>
    public string? DecisionTableId { get; init; }

    /// <summary>Gets the decision table code.</summary>
    public string? DecisionTableCode { get; init; }

    /// <summary>Gets a value indicating whether to fail when no row matches.</summary>
    public bool FailOnNoMatch { get; init; } = true;

    // Type B-4: Sub Flow
    /// <summary>Gets the sub-flow workflow code.</summary>
    public string? SubFlowCode { get; init; }

    /// <summary>Gets the input mappings for the sub-flow.</summary>
    public IReadOnlyList<SubFlowInputMapping> InputMappings { get; init; } = [];

    /// <summary>Gets the output mappings for the sub-flow.</summary>
    public IReadOnlyList<SubFlowOutputMapping> OutputMappings { get; init; } = [];

    // Type B-5: Connector
    /// <summary>Gets the connector type identifier.</summary>
    public string? ConnectorType { get; init; }

    /// <summary>Gets the connector configuration payload.</summary>
    public JsonElement? ConnectorConfig { get; init; }

    /// <summary>Gets the credential identifier for the connector.</summary>
    public string? CredentialId { get; init; }

    /// <summary>Gets the connector resilience options.</summary>
    public ConnectorResilienceOptions? ResilienceOptions { get; init; }

    // Graph routing metadata
    /// <summary>Gets the incoming graph edges for this node.</summary>
    public IReadOnlyList<RuleGraphIncomingEdge> IncomingEdges { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether this node has at least one unconditional outgoing edge.
    /// </summary>
    public bool HasAlwaysEdge { get; init; }

    /// <summary>Gets a value indicating whether this node has an on-false outgoing edge.</summary>
    public bool HasOnFalseEdge { get; init; }

    /// <summary>Gets a value indicating whether this node has an on-error outgoing edge.</summary>
    public bool HasOnErrorEdge { get; init; }
}

/// <summary>Represents a FEEL output field mapping.</summary>
public sealed class FeelOutputField
{
    /// <summary>Gets the output fact path.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Gets the FEEL value expression.</summary>
    public string ValueExpression { get; init; } = string.Empty;

    /// <summary>Gets the output data type.</summary>
    public string DataType { get; init; } = "string";
}

/// <summary>Maps a parent scope path to a child trigger input key.</summary>
public sealed class SubFlowInputMapping
{
    /// <summary>Gets the source path in the parent scope.</summary>
    public string SourcePath { get; init; } = string.Empty;  // parent fact/context path

    /// <summary>Gets the target path in the child scope.</summary>
    public string TargetPath { get; init; } = string.Empty;  // child trigger input key

    /// <summary>Gets the optional transform expression.</summary>
    public string? TransformExpression { get; init; }         // optional FEEL transform
}

/// <summary>Maps a child output fact key to a parent fact key.</summary>
public sealed class SubFlowOutputMapping
{
    /// <summary>Gets the child output path.</summary>
    public string ChildPath { get; init; } = string.Empty;   // child output fact key

    /// <summary>Gets the parent output path.</summary>
    public string ParentPath { get; init; } = string.Empty;  // parent fact key to write

    /// <summary>Gets a value indicating whether the mapping is exposed to the parent.</summary>
    public bool ExposeToParent { get; init; } = true;
}

/// <summary>Resilience options for connector execution.</summary>
public sealed class ConnectorResilienceOptions
{
    /// <summary>Gets the retry count.</summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>Gets the retry delay in seconds.</summary>
    public int RetryDelaySeconds { get; init; } = 1;

    /// <summary>Gets the timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Gets the circuit breaker threshold.</summary>
    public double CircuitBreakerThreshold { get; init; } = 0.5;
}

/// <summary>Represents an incoming edge for a flow graph node.</summary>
public sealed class RuleGraphIncomingEdge
{
    /// <summary>Gets the source node identifier.</summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>Gets the edge type (always, on-true, on-false, on-error).</summary>
    public string EdgeType { get; init; } = "always";
}
