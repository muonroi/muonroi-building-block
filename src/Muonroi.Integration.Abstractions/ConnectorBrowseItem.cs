namespace Muonroi.Integration.Abstractions;

/// <summary>
/// A discoverable document item returned by <see cref="IServiceTaskConnector.ListDocumentsAsync"/>.
/// <para>
/// <see cref="ExternalId"/> is the platform's stable identifier (Jira issue key, Confluence page id,
/// Notion page id, etc.) — it becomes the <c>sourceRef</c> on ingest so the caller never constructs
/// a platform-specific key manually.
/// </para>
/// </summary>
/// <param name="ExternalId">Platform-stable identifier (becomes sourceRef on ingest).</param>
/// <param name="Title">Human-readable title of the document.</param>
/// <param name="Type">
/// Document kind. Known values: "issue", "page", "wiki", "file", "work-item".
/// Additional platform-specific values may be introduced by future presets.
/// </param>
/// <param name="LastModified">Last-modified timestamp, if available from the remote platform.</param>
/// <param name="Author">Display name of the creator or last editor, if available.</param>
/// <param name="Url">Direct URL to the document on the remote platform, if available.</param>
/// <param name="Breadcrumb">
/// Breadcrumb path shown in the document picker row.
/// Example: "Workspace › Space" or "Org › Project".
/// </param>
public sealed record ConnectorBrowseItem(
    string ExternalId,
    string Title,
    string Type,
    DateTimeOffset? LastModified,
    string? Author,
    string? Url,
    string? Breadcrumb
);
