namespace Muonroi.RuleEngine.Proliferation.Models;

/// <summary>
/// Authentication strategy for external project execution requests.
/// Values intentionally match AuthStrategyDto in the control-plane so a simple (int) cast converts between them.
/// </summary>
public enum AuthStrategy
{
    None = 0,
    StaticHeaders = 1,
    OAuth2ClientCredentials = 2,
    MutualTls = 3
}

/// <summary>
/// OAuth2 client credentials configuration for token-based authentication.
/// Used when AuthStrategy is OAuth2ClientCredentials.
/// </summary>
public sealed record OAuth2Config
{
    /// <summary>Token endpoint URL (e.g., "https://auth.example.com/oauth/token").</summary>
    public required string TokenEndpoint { get; init; }

    /// <summary>OAuth2 client_id for the client_credentials flow.</summary>
    public required string ClientId { get; init; }

    /// <summary>OAuth2 client_secret for the client_credentials flow.</summary>
    public required string ClientSecret { get; init; }

    /// <summary>Optional space-separated scopes to request.</summary>
    public string? Scope { get; init; }

    /// <summary>Optional audience claim for the token request.</summary>
    public string? Audience { get; init; }
}

/// <summary>
/// Mutual TLS configuration for certificate-based authentication.
/// Used when AuthStrategy is MutualTls.
/// </summary>
public sealed record MtlsConfig
{
    /// <summary>Path to the PEM or PFX client certificate file.</summary>
    public required string CertificatePath { get; init; }

    /// <summary>Password for PFX certificates. Null for PEM files.</summary>
    public string? CertificatePassword { get; init; }

    /// <summary>Optional path to CA certificate for custom trust anchors.</summary>
    public string? CaCertificatePath { get; init; }
}

/// <summary>
/// API key rotation configuration for automatic key refresh.
/// Works alongside any strategy that uses execution headers.
/// </summary>
public sealed record ApiKeyRotationConfig
{
    /// <summary>When the current API key expires. Null = no expiry tracking.</summary>
    public DateTimeOffset? ApiKeyExpiresAt { get; init; }

    /// <summary>URL to call for API key rotation when expiry is approaching. Null = no auto-rotation.</summary>
    public string? ApiKeyRotationUrl { get; init; }
}

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

    /// <summary>Auth strategy for execution requests. Defaults to StaticHeaders for backward compatibility.</summary>
    public AuthStrategy AuthStrategy { get; init; } = AuthStrategy.StaticHeaders;

    /// <summary>OAuth2 client credentials config. Non-null when AuthStrategy is OAuth2ClientCredentials.</summary>
    public OAuth2Config? OAuth2 { get; init; }

    /// <summary>Mutual TLS config. Non-null when AuthStrategy is MutualTls.</summary>
    public MtlsConfig? Mtls { get; init; }

    /// <summary>Optional API key rotation config. Works alongside any strategy that uses headers.</summary>
    public ApiKeyRotationConfig? ApiKeyRotation { get; init; }
}
