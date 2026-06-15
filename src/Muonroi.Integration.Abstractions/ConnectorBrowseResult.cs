namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Result returned by <see cref="IServiceTaskConnector.ListDocumentsAsync"/>.
/// <para>
/// A null return value from <c>ListDocumentsAsync</c> means the preset does not support browse
/// (honest degrade). A non-null result with <see cref="IsPermissionDenied"/> true means the
/// credential exists but lacks read access on the remote platform — this is a distinct UI state
/// from "no items matched the query".
/// </para>
/// </summary>
public sealed record ConnectorBrowseResult(
    /// <summary>Items returned for the current page. Empty when no matches or permission denied.</summary>
    IReadOnlyList<ConnectorBrowseItem> Items,

    /// <summary>
    /// Opaque cursor to pass as <see cref="ConnectorBrowseQuery.Cursor"/> for the next page.
    /// Null when there are no further pages.
    /// </summary>
    string? NextCursor,

    /// <summary>
    /// True when the connected credential exists but the remote platform returned 401 or 403.
    /// The browse endpoint maps this to category="no-permission" — never conflate with no-match.
    /// </summary>
    bool IsPermissionDenied
);
