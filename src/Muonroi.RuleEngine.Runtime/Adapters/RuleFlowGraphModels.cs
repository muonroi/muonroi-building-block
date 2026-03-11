using System.Text.Json.Serialization;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// C# DTO representation of MRuleFlowGraph JSON produced by the Rule Studio frontend.
/// Property names match the camelCase JSON keys from the TypeScript model.
/// </summary>
internal sealed class RuleFlowGraph
{
    [JsonPropertyName("nodes")]
    public List<RuleFlowNode> Nodes { get; init; } = [];

    [JsonPropertyName("edges")]
    public List<RuleFlowEdge> Edges { get; init; } = [];
}

internal sealed class RuleFlowRuleSetEnvelope
{
    [JsonPropertyName("flowGraph")]
    public RuleFlowGraph? FlowGraph { get; init; }
}

internal sealed class RuleFlowNode
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("ruleCode")]
    public string? RuleCode { get; init; }

    [JsonPropertyName("data")]
    public RuleFlowNodeData Data { get; init; } = new();
}

internal sealed class RuleFlowNodeData
{
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("ruleCode")]
    public string? RuleCode { get; init; }

    [JsonPropertyName("contractRef")]
    public RuleFlowContractReference? ContractRef { get; init; }

    [JsonPropertyName("order")]
    public int? Order { get; init; }

    [JsonPropertyName("dependsOn")]
    public string[]? DependsOn { get; init; }

    [JsonPropertyName("expression")]
    public RuleFlowExpression? Expression { get; init; }

    [JsonPropertyName("liquidConfig")]
    public RuleFlowLiquidConfig? LiquidConfig { get; init; }

    [JsonPropertyName("decisionTableId")]
    public string? DecisionTableId { get; init; }

    [JsonPropertyName("decisionTableCode")]
    public string? DecisionTableCode { get; init; }

    [JsonPropertyName("failOnNoMatch")]
    public bool? FailOnNoMatch { get; init; }

    [JsonPropertyName("subFlowConfig")]
    public RuleFlowSubFlowConfig? SubFlowConfig { get; init; }
}

internal sealed class RuleFlowContractReference
{
    [JsonPropertyName("sourceType")]
    public string? SourceType { get; init; }

    [JsonPropertyName("sourceCode")]
    public string? SourceCode { get; init; }
}

internal sealed class RuleFlowExpression
{
    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

internal sealed class RuleFlowLiquidConfig
{
    [JsonPropertyName("outputFormat")]
    public string OutputFormat { get; init; } = "text";

    [JsonPropertyName("outputFactKey")]
    public string? OutputFactKey { get; init; }
}

internal sealed class RuleFlowSubFlowConfig
{
    [JsonPropertyName("targetFlowCode")]
    public string TargetFlowCode { get; init; } = string.Empty;

    [JsonPropertyName("inputMappings")]
    public List<RuleFlowInputMapping> InputMappings { get; init; } = [];

    [JsonPropertyName("outputMappings")]
    public List<RuleFlowOutputMapping> OutputMappings { get; init; } = [];
}

internal sealed class RuleFlowInputMapping
{
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; init; } = string.Empty;

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; init; } = string.Empty;

    [JsonPropertyName("transformExpression")]
    public string? TransformExpression { get; init; }
}

internal sealed class RuleFlowOutputMapping
{
    [JsonPropertyName("childPath")]
    public string ChildPath { get; init; } = string.Empty;

    [JsonPropertyName("parentPath")]
    public string ParentPath { get; init; } = string.Empty;

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; init; } = string.Empty;

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; init; } = string.Empty;

    [JsonPropertyName("exposeToParent")]
    public bool ExposeToParent { get; init; } = true;
}

internal sealed class RuleFlowEdge
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("edgeType")]
    public string? EdgeType { get; init; }
}
