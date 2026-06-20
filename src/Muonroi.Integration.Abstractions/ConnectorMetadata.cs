namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Metadata for UI discovery and connector catalog display.
/// </summary>
public sealed class ConnectorMetadata
{
    /// <summary>Unique connector type key used for registry resolution.</summary>
    public required string Type { get; init; }
    /// <summary>Display name shown in UI catalogs.</summary>
    public required string DisplayName { get; init; }
    /// <summary>Category used to group connectors in UI.</summary>
    public required string Category { get; init; }
    /// <summary>SVG icon markup for display.</summary>
    public required string IconSvg { get; init; }
    /// <summary>Optional descriptive text for the connector.</summary>
    public string? Description { get; init; }
    /// <summary>Whether credentials are required for execution.</summary>
    public bool RequiresCredentials { get; init; }
    /// <summary>Format registry key for source-content normalization ("adf", "xhtml", "json-path", "markdown", "plain"). Null for non-ingestion connectors.</summary>
    public string? Format { get; init; }
    /// <summary>Ordered form-field descriptors. Null = advanced raw JSON textarea (preserved).</summary>
    public IReadOnlyList<ConnectorFieldDescriptor>? FieldSchema { get; init; }
    /// <summary>Ordered credential-capture field descriptors. Null = no credential form.</summary>
    public IReadOnlyList<ConnectorFieldDescriptor>? CredentialFields { get; init; }
    /// <summary>
    /// Server-side auth header builder key. Values:
    ///   "bearer"        → Authorization: Bearer {token}
    ///   "basic-email"   → Authorization: Basic base64(email:token)  (Jira/Confluence Cloud)
    ///   "basic-pat"     → Authorization: Basic base64("":PAT)       (Azure DevOps)
    ///   "apikey"        → {apiKeyHeader}: {apiKey}
    /// Null = no auth required.
    /// </summary>
    public string? AuthBuilder { get; init; }
}
