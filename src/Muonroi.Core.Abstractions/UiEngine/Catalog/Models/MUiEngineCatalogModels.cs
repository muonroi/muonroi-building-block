namespace Muonroi.UiEngine.Catalog.Models;

public sealed class MUiEngineCatalogApiDescriptor
{
    public string Route { get; set; } = "/";
    public string HttpMethod { get; set; } = "GET";
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string? RequestType { get; set; }
    public string? ResponseType { get; set; }
    public List<string> AuthSchemes { get; set; } = [];
    public List<string> Policies { get; set; } = [];
    public bool RequiresTenantId { get; set; }
}

public sealed class MUiEngineCatalogRuleDescriptor
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<string> DependsOn { get; set; } = [];
    public string HookPoint { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string ContextType { get; set; } = string.Empty;
    public string Source { get; set; } = "code-first";
    public bool IsEnabled { get; set; } = true;
    public bool IsCompensatable { get; set; }
}

public sealed class MUiEngineCatalogBinding
{
    public string EndpointRoute { get; set; } = "/";
    public string HttpMethod { get; set; } = "GET";
    public string? ContextType { get; set; }
    public string? WorkflowName { get; set; }
    public List<MUiEngineCatalogRuleRef> Rules { get; set; } = [];
}

public sealed class MUiEngineCatalogRuleRef
{
    public string Code { get; set; } = string.Empty;
    public int Order { get; set; }
    public string HookPoint { get; set; } = string.Empty;
}

public sealed class MUiEngineCatalogGraph
{
    public List<MUiEngineCatalogGraphNode> Nodes { get; set; } = [];
    public List<MUiEngineCatalogGraphEdge> Edges { get; set; } = [];
}

public sealed class MUiEngineCatalogGraphNode
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "endpoint";
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, string?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MUiEngineCatalogGraphEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string EdgeType { get; set; } = "triggers";
}
