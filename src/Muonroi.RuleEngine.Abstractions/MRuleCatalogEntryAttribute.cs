namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Provides palette metadata for a rule exposed to authoring tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MRuleCatalogEntryAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the BA-facing display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the palette category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the icon key for the palette.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets searchable tags for the palette item.
    /// </summary>
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the long-form description for the palette item.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry should be visible in the palette.
    /// </summary>
    public bool IsPaletteVisible { get; set; } = true;
}
