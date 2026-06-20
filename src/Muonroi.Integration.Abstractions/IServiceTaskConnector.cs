using System.Text.Json;

namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Core connector contract. Each connector type (HTTP, Email, Slack, etc.)
/// implements this interface and is registered in the <see cref="IConnectorRegistry"/>.
/// </summary>
public interface IServiceTaskConnector
{
    /// <summary>Connector metadata used by the registry and UI.</summary>
    ConnectorMetadata Metadata { get; }
    /// <summary>Executes the connector with the provided context.</summary>
    /// <param name="context">Execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct);
    /// <summary>Tests connectivity using the provided context.</summary>
    /// <param name="context">Execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct);
    /// <summary>Returns a JSON schema describing the connector configuration.</summary>
    JsonElement GetConfigSchema();

    /// <summary>
    /// Discovers documents available to the connected credential.
    /// The remote query (JQL / CQL / Notion search / etc.) is built server-side inside each
    /// preset — the BA never supplies raw platform query syntax.
    /// <para>
    /// Returns <see langword="null"/> when this preset does not support browse (graceful degrade).
    /// The browse endpoint maps null to <c>{ "supported": false }</c> — never a 500.
    /// </para>
    /// </summary>
    /// <param name="context">Connector execution context (config, credentials, tenant).</param>
    /// <param name="query">Typed browse query (SearchText / Scope / TypeFilter / paging).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ConnectorBrowseResult"/> with the discovered items and paging cursor,
    /// or <see langword="null"/> if browse is not supported by this preset.
    /// </returns>
    Task<ConnectorBrowseResult?> ListDocumentsAsync(
        ConnectorContext context,
        ConnectorBrowseQuery query,
        CancellationToken ct)
        => Task.FromResult<ConnectorBrowseResult?>(null); // default: browse not supported

    /// <summary>
    /// Fetches a single document by its external reference (page ID, issue key, etc.)
    /// using the preset-specific REST API — Confluence Server: <c>/rest/api/content/{id}?expand=body.storage</c>;
    /// Jira Server: <c>/rest/api/2/issue/{key}?fields=summary,description</c>.
    /// <para>
    /// Returns <see langword="null"/> when this preset does not support per-document fetch (graceful
    /// degrade). A null result instructs the ingestion pipeline to fall back to the legacy
    /// <c>BuildConfigWithSourceRef + ExecuteAsync</c> path — all presets that do not override this
    /// method continue to work byte-for-byte unchanged.
    /// </para>
    /// <para>
    /// Implementations MUST NOT log the document body (D-06). On permission-denied or parse failure
    /// they MUST return a <see cref="ConnectorDocumentContent"/> with an empty <c>Body</c> rather
    /// than throwing, so the caller always stores an empty-but-safe record instead of a 500.
    /// </para>
    /// </summary>
    /// <param name="context">Connector execution context (config, credentials, tenant).</param>
    /// <param name="sourceRef">External document identifier (page ID, issue key, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ConnectorDocumentContent"/> containing the raw body and its normalizer format key,
    /// or <see langword="null"/> if this preset does not support direct document fetch.
    /// </returns>
    Task<ConnectorDocumentContent?> FetchDocumentAsync(
        ConnectorContext context,
        string sourceRef,
        CancellationToken ct)
        => Task.FromResult<ConnectorDocumentContent?>(null); // default: use legacy ExecuteAsync path

    /// <summary>
    /// Enumerates the scopes (projects, spaces, etc.) available to the connected credential.
    /// The BA selects a scope to narrow document browse results.
    /// <para>
    /// Returns <see langword="null"/> when this preset does not support scope enumeration (graceful degrade).
    /// The scopes endpoint maps null to <c>{ "supported": false }</c> — never a 500.
    /// </para>
    /// </summary>
    /// <param name="context">Connector execution context (config, credentials, tenant).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A read-only list of <see cref="ConnectorScope"/> items available to the credential,
    /// or <see langword="null"/> if scope enumeration is not supported by this preset.
    /// Returns an empty list (not null) when the preset supports enumeration but the call
    /// failed or returned no results.
    /// </returns>
    Task<IReadOnlyList<ConnectorScope>?> ListScopesAsync(
        ConnectorContext context,
        CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ConnectorScope>?>(null); // default: scopes not supported
}
