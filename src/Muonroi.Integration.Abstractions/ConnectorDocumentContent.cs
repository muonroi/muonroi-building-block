namespace Muonroi.Integration.Abstractions;

/// <summary>
/// The body and normalizer format key returned by a preset-specific document fetch.
/// Produced by <see cref="IServiceTaskConnector.FetchDocumentAsync"/> implementations;
/// consumed by the ingestion pipeline to select the correct normalizer without guessing
/// from the connector type.
/// </summary>
/// <param name="Body">
/// Raw document body: XHTML storage value for Confluence Server; summary + description
/// text for Jira Server. Never null — empty string when the body could not be retrieved.
/// Never logged; never persisted raw (D-06).
/// </param>
/// <param name="Format">
/// Normalizer format key understood by <c>ISourceFormatNormalizerRegistry.ResolveByFormat</c>.
/// Typical values: <c>"xhtml"</c> (Confluence Server), <c>"plain"</c> (Jira Server),
/// <c>"adf"</c> (Jira Cloud). The key is defined by the preset that best knows the wire format.
/// </param>
public sealed record ConnectorDocumentContent(string Body, string Format);
