using System.Collections.Generic;

namespace Muonroi.RuleEngine.SourceGenerators.Authoring;

internal sealed record AuthoringManifestDefinition(
    string AssemblyName,
    string AssemblyVersion,
    IReadOnlyList<AuthoringRuleDefinition> Rules);

internal sealed record AuthoringRuleDefinition(
    string Code,
    int Order,
    IReadOnlyList<string> DependsOn,
    string? ContextTypeName,
    string? ContextTitle,
    string? ContextDescription,
    AuthoringSchemaNode? ContextSchema,
    IReadOnlyList<AuthoringFactDefinition> ConsumedFacts,
    IReadOnlyList<AuthoringFactDefinition> ProducedFacts,
    string? DisplayName,
    string? Category,
    string? Icon,
    IReadOnlyList<string> Tags,
    string? Description,
    bool IsPaletteVisible);

internal sealed record AuthoringFactDefinition(
    string Key,
    string? Label,
    string? ClrTypeName,
    string? Description,
    string? Example,
    AuthoringSchemaNode? Schema);

internal sealed record AuthoringSchemaNode(
    string TypeName,
    bool IsNullable,
    string? Description,
    IReadOnlyList<AuthoringSchemaField> Fields);

internal sealed record AuthoringSchemaField(
    string Path,
    string Label,
    string DataType,
    bool Required,
    string? Description,
    string? Example,
    IReadOnlyList<AuthoringSchemaField> Children);

internal sealed record AuthoringFactOverride(
    string FactKey,
    string? Label,
    string? Description,
    string? Example);

internal sealed record AuthoringContextOverride(
    string? Title,
    string? Description);

internal sealed record CatalogMetadataDefinition(
    string? DisplayName,
    string? Category,
    string? Icon,
    IReadOnlyList<string> Tags,
    string? Description,
    bool? IsPaletteVisible);
