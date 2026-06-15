namespace Muonroi.Integration.Abstractions;

/// <summary>
/// A discoverable document item returned by <see cref="IServiceTaskConnector.ListDocumentsAsync"/>.
/// <para>
/// <see cref="ExternalId"/> is the platform's stable identifier (Jira issue key, Confluence page id,
/// Notion page id, etc.) — it becomes the <c>sourceRef</c> on ingest so the caller never constructs
/// a platform-specific key manually.
/// </para>
/// </summary>
public sealed record ConnectorBrowseItem(
    /// <summary>Platform-stable identifier (becomes sourceRef on ingest).</summary>
    string ExternalId,

    /// <summary>Human-readable title of the document.</summary>
    string Title,

    /// <summary>
    /// Document kind. Known values: "issue", "page", "wiki", "file", "work-item".
    /// Additional platform-specific values may be introduced by future presets.
    /// </summary>
    string Type,

    /// <summary>Last-modified timestamp, if available from the remote platform.</summary>
    DateTimeOffset? LastModified,

    /// <summary>Display name of the creator or last editor, if available.</summary>
    string? Author,

    /// <summary>Direct URL to the document on the remote platform, if available.</summary>
    string? Url,

    /// <summary>
    /// Breadcrumb path shown in the document picker row.
    /// Example: "Workspace › Space" or "Org › Project".
    /// </summary>
    string? Breadcrumb
);
