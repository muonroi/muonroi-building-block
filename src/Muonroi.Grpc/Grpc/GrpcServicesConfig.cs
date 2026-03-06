namespace Muonroi.Grpc.Grpc;

public class GrpcServicesConfig
{
    public GrpcServerConfig Server { get; set; } = new();
    public GrpcClientDefaultsConfig ClientDefaults { get; set; } = new();
    public Dictionary<string, GrpcServiceConfig>? Services { get; set; } = [];
}

public class GrpcServiceConfig
{
    public string Uri { get; set; } = string.Empty;
    public int? TimeoutSeconds { get; set; }
    public int? RetryCount { get; set; }
    public int? InitialBackoffSeconds { get; set; }
    public int? MaxBackoffSeconds { get; set; }
    public string? LoadBalancingPolicy { get; set; }
    public int? MaxReceiveMessageSizeBytes { get; set; }
    public int? MaxSendMessageSizeBytes { get; set; }
    public Dictionary<string, GrpcMethodPolicyConfig>? Methods { get; set; } = [];
}

public class GrpcMethodPolicyConfig
{
    public int? TimeoutSeconds { get; set; }
    public int? RetryCount { get; set; }
    public int? InitialBackoffSeconds { get; set; }
    public int? MaxBackoffSeconds { get; set; }
}

public class GrpcClientDefaultsConfig
{
    public int TimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 3;
    public int InitialBackoffSeconds { get; set; } = 1;
    public int MaxBackoffSeconds { get; set; } = 8;
    public string LoadBalancingPolicy { get; set; } = "pick_first";
    public int MaxReceiveMessageSizeBytes { get; set; } = 100 * 1024 * 1024;
    public int MaxSendMessageSizeBytes { get; set; } = 100 * 1024 * 1024;
}

public class GrpcServerConfig
{
    public bool EnableDetailedErrors { get; set; } = true;
    public int MaxSendMessageSizeBytes { get; set; } = 100 * 1024 * 1024;
    public int MaxReceiveMessageSizeBytes { get; set; } = 100 * 1024 * 1024;
    public string ResponseCompressionAlgorithm { get; set; } = "gzip";
    public string ResponseCompressionLevel { get; set; } = "Optimal";
    public bool EnableGrpcWeb { get; set; }
    public bool EnableGrpcWebForAllServices { get; set; }
    public bool EnableJsonTranscoding { get; set; }
    public bool RequireMutualTls { get; set; }
    public string[] AllowedClientCertificateThumbprints { get; set; } = [];
    public GrpcRateLimitConfig RateLimit { get; set; } = new();
}

public class GrpcRateLimitConfig
{
    public bool Enabled { get; set; }
    public int RequestsPerMinutePerApiKey { get; set; } = 600;
    public int RequestsPerMinutePerTenant { get; set; } = 1200;
}

internal static class GrpcServicesConfigBinding
{
    public static GrpcServicesConfig Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

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
