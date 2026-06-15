namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Describes a single form field for connector configuration.
/// Used to render a schema-driven form in the dashboard instead of a raw JSON textarea.
/// </summary>
public sealed record ConnectorFieldDescriptor
{
    /// <summary>The configJson property key.</summary>
    public required string Key { get; init; }
    /// <summary>UI label text.</summary>
    public required string Label { get; init; }
    /// <summary>Input type: "text" | "url" | "password" | "number".</summary>
    public required string FieldType { get; init; }
    /// <summary>Optional placeholder hint displayed in the input.</summary>
    public string? Placeholder { get; init; }
    /// <summary>Whether the field is required before the connector can be saved.</summary>
    public bool Required { get; init; }
}
