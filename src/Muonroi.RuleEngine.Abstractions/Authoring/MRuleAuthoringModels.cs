namespace Muonroi.RuleEngine.Abstractions.Authoring;

/// <summary>
/// Represents the authoring manifest discovered for one assembly.
/// </summary>
public sealed class MRuleAuthoringManifest
{
    /// <summary>
    /// Gets the assembly name that produced the manifest.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assembly version that produced the manifest.
    /// </summary>
    public string AssemblyVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the authoring entries discovered in the assembly.
    /// </summary>
    public IReadOnlyList<MRuleAuthoringEntry> Rules { get; init; } = [];
}

/// <summary>
/// Describes one rule that can be surfaced to authoring experiences.
/// </summary>
public sealed class MRuleAuthoringEntry
{
    /// <summary>
    /// Gets the unique rule code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the relative execution order hint.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the rule codes that must run before this rule.
    /// </summary>
    public string[] DependsOn { get; init; } = [];

    /// <summary>
    /// Gets the CLR type name of the rule context.
    /// </summary>
    public string? ContextTypeName { get; init; }

    /// <summary>
    /// Gets the authoring title for the context.
    /// </summary>
    public string? ContextTitle { get; init; }

    /// <summary>
    /// Gets the authoring description for the context.
    /// </summary>
    public string? ContextDescription { get; init; }

    /// <summary>
    /// Gets the context schema used by the rule authoring UI.
    /// </summary>
    public MFactSchemaNode? ContextSchema { get; init; }

    /// <summary>
    /// Gets the facts consumed by the rule.
    /// </summary>
    public IReadOnlyList<MFactEntry> ConsumedFacts { get; init; } = [];

    /// <summary>
    /// Gets the facts produced by the rule.
    /// </summary>
    public IReadOnlyList<MFactEntry> ProducedFacts { get; init; } = [];

    /// <summary>
    /// Gets the BA-facing display name shown in the palette.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the palette category used to group the rule.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the icon key rendered by the palette.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets searchable tags associated with the rule.
    /// </summary>
    public string[] Tags { get; init; } = [];

    /// <summary>
    /// Gets the tooltip or long-form description for the palette entry.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether the rule should appear in the authoring palette.
    /// </summary>
    public bool IsPaletteVisible { get; init; } = true;
}

/// <summary>
/// Describes one fact consumed or produced by a rule.
/// </summary>
public sealed class MFactEntry
{
    /// <summary>
    /// Gets the fact key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display label used in authoring UIs.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets the CLR type name for the fact value.
    /// </summary>
    public string? ClrTypeName { get; init; }

    /// <summary>
    /// Gets the fact description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an example value for the fact.
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// Gets the schema for the fact payload.
    /// </summary>
    public MFactSchemaNode? Schema { get; init; }
}

/// <summary>
/// Represents a recursive schema node for authoring contracts.
/// </summary>
public sealed class MFactSchemaNode
{
    /// <summary>
    /// Gets the root CLR type name.
    /// </summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the node is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets an optional authoring description for the node.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets child fields under the node.
    /// </summary>
    public IReadOnlyList<MFactSchemaField> Fields { get; init; } = [];
}

/// <summary>
/// Describes one flattened schema field exposed to authoring UIs.
/// </summary>
public sealed class MFactSchemaField
{
    /// <summary>
    /// Gets the flattened field path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the normalized field data type.
    /// </summary>
    public string DataType { get; init; } = "string";

    /// <summary>
    /// Gets a value indicating whether the field is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the field description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an example value for the field.
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// Gets child fields under the current field.
    /// </summary>
    public IReadOnlyList<MFactSchemaField> Children { get; init; } = [];
}
