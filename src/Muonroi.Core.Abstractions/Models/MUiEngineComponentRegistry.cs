namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Registry for UI engine components.
/// </summary>
public sealed class MUiEngineComponentRegistry
{
    /// <summary>
    /// The registered components.
    /// </summary>
    public Dictionary<string, MUiEngineComponentDescriptor> Components { get; set; } = [];
}

/// <summary>
/// Descriptor for a UI engine component.
/// </summary>
public sealed class MUiEngineComponentDescriptor
{
    /// <summary>
    /// The component type.
    /// </summary>
    public string ComponentType { get; set; } = string.Empty;
    /// <summary>
    /// The bundle URL.
    /// </summary>
    public string BundleUrl { get; set; } = string.Empty;
    /// <summary>
    /// The CSS URL.
    /// </summary>
    public string? CssUrl { get; set; }
    /// <summary>
    /// The custom element tag.
    /// </summary>
    public string? CustomElementTag { get; set; }
    /// <summary>
    /// Whether the component is lazy-loaded.
    /// </summary>
    public bool IsLazyLoaded { get; set; } = true;
    /// <summary>
    /// The required license tier.
    /// </summary>
    public string RequiredTier { get; set; } = "Free";
}
