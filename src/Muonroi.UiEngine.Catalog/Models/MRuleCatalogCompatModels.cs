namespace Muonroi.UiEngine.Catalog.Models;

/// <summary>
/// A catalog item in the format expected by <c>MRuleCatalogService</c> frontend.
/// Maps from <see cref="MUiEngineCatalogRuleDescriptor"/> to the JSON shape
/// that <c>mu-rule-flow-designer</c> Palette consumes.
/// </summary>
public sealed record MRuleCatalogItemResponse(
    string Code,
    string DisplayName,
    string? Category,
    string? Description,
    string? Icon,
    IReadOnlyList<string>? Tags,
    object? InputSchema,
    object? OutputSchema
);

/// <summary>
/// A group of catalog items by category, matching frontend <c>MRuleCatalogGroup</c>.
/// </summary>
public sealed record MRuleCatalogGroupResponse(
    string Category,
    IReadOnlyList<MRuleCatalogItemResponse> Items
);
