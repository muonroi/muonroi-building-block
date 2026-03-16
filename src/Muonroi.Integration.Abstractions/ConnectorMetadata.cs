namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Metadata for UI discovery and connector catalog display.
/// </summary>
public sealed class ConnectorMetadata
{
    public required string Type { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required string IconSvg { get; init; }
    public string? Description { get; init; }
    public bool RequiresCredentials { get; init; }
}
