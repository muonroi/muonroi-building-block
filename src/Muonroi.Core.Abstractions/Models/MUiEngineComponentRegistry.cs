namespace Muonroi.Core.Abstractions.Models;

public sealed class MUiEngineComponentRegistry
{
    public Dictionary<string, MUiEngineComponentDescriptor> Components { get; set; } = [];
}

public sealed class MUiEngineComponentDescriptor
{
    public string ComponentType { get; set; } = string.Empty;
    public string BundleUrl { get; set; } = string.Empty;
    public string? CssUrl { get; set; }
    public string? CustomElementTag { get; set; }
    public bool IsLazyLoaded { get; set; } = true;
    public string RequiredTier { get; set; } = "Free";
}
