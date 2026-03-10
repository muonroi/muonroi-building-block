namespace Muonroi.RuleEngine.Abstractions.Authoring;

public sealed class MRuleAuthoringManifest
{
    public string AssemblyName { get; init; } = string.Empty;
    public string AssemblyVersion { get; init; } = string.Empty;
    public IReadOnlyList<MRuleAuthoringEntry> Rules { get; init; } = [];
}

public sealed class MRuleAuthoringEntry
{
    public string Code { get; init; } = string.Empty;
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];
    public string? ContextTypeName { get; init; }
    public string? ContextTitle { get; init; }
    public string? ContextDescription { get; init; }
    public MFactSchemaNode? ContextSchema { get; init; }
    public IReadOnlyList<MFactEntry> ConsumedFacts { get; init; } = [];
    public IReadOnlyList<MFactEntry> ProducedFacts { get; init; } = [];
}

public sealed class MFactEntry
{
    public string Key { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? ClrTypeName { get; init; }
    public string? Description { get; init; }
    public string? Example { get; init; }
    public MFactSchemaNode? Schema { get; init; }
}

public sealed class MFactSchemaNode
{
    public string TypeName { get; init; } = string.Empty;
    public bool IsNullable { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<MFactSchemaField> Fields { get; init; } = [];
}

public sealed class MFactSchemaField
{
    public string Path { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DataType { get; init; } = "string";
    public bool Required { get; init; }
    public string? Description { get; init; }
    public string? Example { get; init; }
    public IReadOnlyList<MFactSchemaField> Children { get; init; } = [];
}
