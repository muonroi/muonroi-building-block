using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Represents the root configuration for Muonroi gRPC services.
/// </summary>
public class GrpcServicesConfig
{
    /// <summary>
    /// Gets or sets the server-side gRPC configuration.
    /// </summary>
    public GrpcServerConfig Server { get; set; } = new();

    /// <summary>
    /// Gets or sets the default client configuration applied to every registered gRPC client.
    /// </summary>
    public GrpcClientDefaultsConfig ClientDefaults { get; set; } = new();

    /// <summary>
    /// Gets or sets the named service-specific gRPC client configuration entries.
    /// </summary>
    public Dictionary<string, GrpcServiceConfig>? Services { get; set; } = [];
}

/// <summary>
/// Represents configuration for a single named gRPC client service.
/// </summary>
public class GrpcServiceConfig
{
    /// <summary>
    /// Gets or sets the service base address.
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the per-call timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the retry count for retriable gRPC failures.
    /// </summary>
    public int? RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the initial retry backoff in seconds.
    /// </summary>
    public int? InitialBackoffSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum retry backoff in seconds.
    /// </summary>
    public int? MaxBackoffSeconds { get; set; }

    /// <summary>
    /// Gets or sets the load balancing policy name.
    /// </summary>
    public string? LoadBalancingPolicy { get; set; }

    /// <summary>
    /// Gets or sets the maximum response message size in bytes.
    /// </summary>
    public int? MaxReceiveMessageSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum request message size in bytes.
    /// </summary>
    public int? MaxSendMessageSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution context access token should be forwarded.
    /// A null value falls back to <see cref="GrpcClientDefaultsConfig.ForwardAuthToken"/>.
    /// </summary>
    public bool? ForwardAuthToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant identifier should be forwarded.
    /// A null value falls back to <see cref="GrpcClientDefaultsConfig.ForwardTenantId"/>.
    /// </summary>
    public bool? ForwardTenantId { get; set; }

    /// <summary>
    /// Gets or sets the per-method policy overrides.
    /// </summary>
    public Dictionary<string, GrpcMethodPolicyConfig>? Methods { get; set; } = [];
}

/// <summary>
/// Represents configuration for an individual gRPC method.
/// </summary>
public class GrpcMethodPolicyConfig
{
    /// <summary>
    /// Gets or sets the per-call timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the retry count for retriable gRPC failures.
    /// </summary>
    public int? RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the initial retry backoff in seconds.
    /// </summary>
    public int? InitialBackoffSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum retry backoff in seconds.
    /// </summary>
    public int? MaxBackoffSeconds { get; set; }
}

/// <summary>
/// Represents default configuration applied to all registered gRPC clients.
/// </summary>
public class GrpcClientDefaultsConfig
{
    /// <summary>
    /// Gets or sets the default per-call timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default retry count for retriable gRPC failures.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the default initial retry backoff in seconds.
    /// </summary>
    public int InitialBackoffSeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the default maximum retry backoff in seconds.
    /// </summary>
    public int MaxBackoffSeconds { get; set; } = 8;

    /// <summary>
    /// Gets or sets the default load balancing policy.
    /// </summary>
    public string LoadBalancingPolicy { get; set; } = "pick_first";

    /// <summary>
    /// Gets or sets the default maximum response message size in bytes.
    /// </summary>
    public int MaxReceiveMessageSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the default maximum request message size in bytes.
    /// </summary>
    public int MaxSendMessageSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether the execution context access token should be forwarded by default.
    /// </summary>
    public bool ForwardAuthToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant identifier should be forwarded by default.
    /// </summary>
    public bool ForwardTenantId { get; set; } = true;
}

/// <summary>
/// Represents configuration for the gRPC server runtime.
/// </summary>
public class GrpcServerConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether detailed errors are enabled.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum response message size in bytes.
    /// </summary>
    public int MaxSendMessageSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum request message size in bytes.
    /// </summary>
    public int MaxReceiveMessageSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the response compression algorithm.
    /// </summary>
    public string ResponseCompressionAlgorithm { get; set; } = "gzip";

    /// <summary>
    /// Gets or sets the response compression level.
    /// </summary>
    public string ResponseCompressionLevel { get; set; } = "Optimal";

    /// <summary>
    /// Gets or sets a value indicating whether gRPC-Web is enabled.
    /// </summary>
    public bool EnableGrpcWeb { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gRPC-Web is enabled for all services.
    /// </summary>
    public bool EnableGrpcWebForAllServices { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether JSON transcoding is enabled.
    /// </summary>
    public bool EnableJsonTranscoding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether mutual TLS is required.
    /// </summary>
    public bool RequireMutualTls { get; set; }

    /// <summary>
    /// Gets or sets the allowed client certificate thumbprints.
    /// </summary>
    public string[] AllowedClientCertificateThumbprints { get; set; } = [];

    /// <summary>
    /// Gets or sets the server rate limiting configuration.
    /// </summary>
    public GrpcRateLimitConfig RateLimit { get; set; } = new();
}

/// <summary>
/// Represents server-side rate limiting options for gRPC traffic.
/// </summary>
public class GrpcRateLimitConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the requests-per-minute limit per API key.
    /// </summary>
    public int RequestsPerMinutePerApiKey { get; set; } = 600;

    /// <summary>
    /// Gets or sets the requests-per-minute limit per tenant.
    /// </summary>
    public int RequestsPerMinutePerTenant { get; set; } = 1200;
}

internal static class GrpcServicesConfigBinding
{
    public static GrpcServicesConfig Bind(IConfiguration configuration)
    {
        MGuard.NotNull(configuration);

        GrpcServicesConfig config = new();

        // Backward compatible with existing README samples.
        configuration.GetSection(nameof(GrpcServicesConfig)).Bind(config);
        if (config.Services == null || config.Services.Count == 0)
        {
            configuration.GetSection("GrpcServices").Bind(config);
        }

        config.Server ??= new GrpcServerConfig();
        config.ClientDefaults ??= new GrpcClientDefaultsConfig();
        config.Services ??= [];
        return config;
    }
}
