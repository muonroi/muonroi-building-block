namespace Quickstart.Integration.Api.Models;

/// <summary>
/// Request body for the generic connector execute endpoint.
/// </summary>
/// <param name="ConnectorType">
/// Registry key that identifies the connector to run
/// (e.g. "http", "slack", "github", "email", "sql", "redis").
/// </param>
/// <param name="Config">
/// Connector-specific configuration payload.
/// The required fields differ per connector type — call
/// GET /api/connectors/{type} to retrieve the JSON schema for each one.
/// </param>
/// <param name="CorrelationId">
/// Optional caller-supplied correlation ID that is forwarded to the connector
/// and included in the response for end-to-end tracing.
/// </param>
public record ExecuteConnectorRequest(
    string ConnectorType,
    JsonDocument Config,
    string? CorrelationId = null);
