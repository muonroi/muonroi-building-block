namespace Muonroi.UiEngine.Catalog.Models;

/// <summary>
/// Descriptor for a catalog API.
/// </summary>
public sealed class MUiEngineCatalogApiDescriptor
{
    /// <summary>
    /// The API route.
    /// </summary>
    public string Route { get; set; } = "/";
    /// <summary>
    /// The HTTP method.
    /// </summary>
    public string HttpMethod { get; set; } = "GET";
    /// <summary>
    /// The name of the controller.
    /// </summary>
    public string ControllerName { get; set; } = string.Empty;
    /// <summary>
    /// The name of the action.
    /// </summary>
    public string ActionName { get; set; } = string.Empty;
    /// <summary>
    /// The request type.
    /// </summary>
    public string? RequestType { get; set; }
    /// <summary>
    /// The response type.
    /// </summary>
    public string? ResponseType { get; set; }
    /// <summary>
    /// The authentication schemes.
    /// </summary>
    public List<string> AuthSchemes { get; set; } = [];
    /// <summary>
    /// The policies.
    /// </summary>
    public List<string> Policies { get; set; } = [];
    /// <summary>
    /// Whether a tenant ID is required.
    /// </summary>
    public bool RequiresTenantId { get; set; }
}

/// <summary>
/// Descriptor for a catalog rule.
/// </summary>
public sealed class MUiEngineCatalogRuleDescriptor
{
    /// <summary>
    /// The rule code.
    /// </summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// The rule name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The order of execution.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Dependencies for this rule.
    /// </summary>
    public List<string> DependsOn { get; set; } = [];
    /// <summary>
    /// The hook point for the rule.
    /// </summary>
    public string HookPoint { get; set; } = string.Empty;
    /// <summary>
    /// The type of the rule.
    /// </summary>
    public string RuleType { get; set; } = string.Empty;
    /// <summary>
    /// The context type.
    /// </summary>
    public string ContextType { get; set; } = string.Empty;
    /// <summary>
    /// The source of the rule.
    /// </summary>
    public string Source { get; set; } = "code-first";
    /// <summary>
    /// Whether the rule is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    /// <summary>
    /// Whether the rule is compensatable.
    /// </summary>
    public bool IsCompensatable { get; set; }
}

/// <summary>
/// Represents a catalog binding.
/// </summary>
public sealed class MUiEngineCatalogBinding
{
    /// <summary>
    /// The endpoint route.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";
    /// <summary>
    /// The HTTP method.
    /// </summary>
    public string HttpMethod { get; set; } = "GET";
    /// <summary>
    /// The context type.
    /// </summary>
    public string? ContextType { get; set; }
    /// <summary>
    /// The name of the workflow.
    /// </summary>
    public string? WorkflowName { get; set; }
    /// <summary>
    /// The rules associated with this binding.
    /// </summary>
    public List<MUiEngineCatalogRuleRef> Rules { get; set; } = [];
}

/// <summary>
/// Reference to a catalog rule.
/// </summary>
public sealed class MUiEngineCatalogRuleRef
{
    /// <summary>
    /// The rule code.
    /// </summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// The order of execution.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// The hook point for the rule.
    /// </summary>
    public string HookPoint { get; set; } = string.Empty;
}

/// <summary>
/// Represents a catalog graph.
/// </summary>
public sealed class MUiEngineCatalogGraph
{
    /// <summary>
    /// The nodes in the graph.
    /// </summary>
    public List<MUiEngineCatalogGraphNode> Nodes { get; set; } = [];
    /// <summary>
    /// The edges in the graph.
    /// </summary>
    public List<MUiEngineCatalogGraphEdge> Edges { get; set; } = [];
}

/// <summary>
/// Represents a node in the catalog graph.
/// </summary>
public sealed class MUiEngineCatalogGraphNode
{
    /// <summary>
    /// The ID of the node.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// The type of the node.
    /// </summary>
    public string Type { get; set; } = "endpoint";
    /// <summary>
    /// The label for the node.
    /// </summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>
    /// Data associated with the node.
    /// </summary>
    public Dictionary<string, string?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents an edge in the catalog graph.
/// </summary>
public sealed class MUiEngineCatalogGraphEdge
{
    /// <summary>
    /// The source node ID.
    /// </summary>
    public string From { get; set; } = string.Empty;
    /// <summary>
    /// The destination node ID.
    /// </summary>
    public string To { get; set; } = string.Empty;
    /// <summary>
    /// The type of the edge.
    /// </summary>
    public string EdgeType { get; set; } = "triggers";
}
