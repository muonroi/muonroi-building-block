namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Typed input for <see cref="IServiceTaskConnector.ListDocumentsAsync"/>.
/// <para>
/// The remote query (JQL / CQL / Notion search body / etc.) is built server-side inside each
/// preset from these fields — the caller never supplies raw platform query syntax.
/// </para>
/// </summary>
/// <param name="SearchText">
/// Free-text search. When null, the preset returns recent/default items
/// for the connected credential (no <c>text ~</c> clause in JQL, etc.).
/// </param>
/// <param name="Scope">
/// Optional scope limiter: Jira project key, Confluence space key, repo name, etc.
/// When null, results span all scopes accessible to the credential.
/// </param>
/// <param name="TypeFilter">
/// Optional type filter: "issue", "page", "wiki", etc.
/// When null, the preset returns all supported types.
/// </param>
/// <param name="Cursor">Opaque paging cursor returned by a previous call. Null for the first page.</param>
/// <param name="PageSize">Maximum number of items to return. Default is 20.</param>
public sealed record ConnectorBrowseQuery(
    string? SearchText,
    string? Scope,
    string? TypeFilter,
    string? Cursor,
    int PageSize = 20
);
