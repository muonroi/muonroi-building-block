namespace Muonroi.RuleEngine.Proliferation.Models;

/// <summary>
/// Configures how external API responses map to pass/fail evaluation.
/// If null/empty, falls back to HTTP status code (2xx=pass, 4xx/5xx=fail).
/// </summary>
public sealed record ResponseMapping
{
    /// <summary>JSON path to boolean success indicator (e.g., "result.success", "status").</summary>
    public string? SuccessField { get; init; }

    /// <summary>Expected value for success comparison (default: "true").</summary>
    public string SuccessValue { get; init; } = "true";

    /// <summary>JSON path to extract output data (e.g., "result.data"). Null = use entire response body.</summary>
    public string? OutputFieldsPath { get; init; }

    /// <summary>JSON path to error message field (e.g., "error.message").</summary>
    public string? ErrorFieldPath { get; init; }
}

/// <summary>
/// Configuration for executing scenarios against an external project's API endpoint.
/// Populated from RegisteredProject at runtime.
/// </summary>
public sealed record ExternalProjectConfig
{
    /// <summary>Unique identifier of the registered external project.</summary>
    public required string ProjectId { get; init; }

    /// <summary>Tenant identifier associated with this external project.</summary>
    public required string TenantId { get; init; }

    /// <summary>URL of the external project's execution endpoint (receives scenario InputFacts as POST body).</summary>
    public required string ExecutionEndpointUrl { get; init; }

    /// <summary>Optional response mapping configuration. Null = HTTP status fallback.</summary>
    public ResponseMapping? ResponseMapping { get; init; }

    /// <summary>Optional HTTP headers to include in execution requests (e.g., API key, authorization).</summary>
    public IReadOnlyDictionary<string, string>? ExecutionHeaders { get; init; }

    /// <summary>Optional webhook URL for post-execution notifications. Null = no webhook.</summary>
    public string? WebhookUrl { get; init; }

    /// <summary>Request timeout in seconds (default: 30).</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
