namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Typed input for <see cref="IServiceTaskConnector.ListDocumentsAsync"/>.
/// <para>
/// The remote query (JQL / CQL / Notion search body / etc.) is built server-side inside each
/// preset from these fields — the caller never supplies raw platform query syntax.
/// </para>
/// </summary>
public sealed record ConnectorBrowseQuery(
    /// <summary>
    /// Free-text search. When null, the preset returns recent/default items
    /// for the connected credential (no <c>text ~</c> clause in JQL, etc.).
    /// </summary>
    string? SearchText,

    /// <summary>
    /// Optional scope limiter: Jira project key, Confluence space key, repo name, etc.
    /// When null, results span all scopes accessible to the credential.
    /// </summary>
    string? Scope,

    /// <summary>
    /// Optional type filter: "issue", "page", "wiki", etc.
    /// When null, the preset returns all supported types.
    /// </summary>
    string? TypeFilter,

    /// <summary>Opaque paging cursor returned by a previous call. Null for the first page.</summary>
    string? Cursor,

    /// <summary>Maximum number of items to return. Default is 20.</summary>
    int PageSize = 20
);
