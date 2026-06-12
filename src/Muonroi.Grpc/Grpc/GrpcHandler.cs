using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;

namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Provides registration helpers for Muonroi gRPC server and client infrastructure.
/// </summary>
public static class GrpcHandler
{
    /// <summary>
    /// Registers Muonroi gRPC server services and server interceptors.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The optional configuration source.</param>
    public static void AddGrpcServer(this IServiceCollection services, IConfiguration? configuration = null)
    {
        MGuard.NotNull(services);
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.Grpc);

        GrpcServicesConfig grpcConfig = configuration is null ? new GrpcServicesConfig() : GrpcServicesConfigBinding.Bind(configuration);

        services.TryAddSingleton(_ => Options.Create(grpcConfig));
        EnsureGrpcClientInfrastructure(services);
        services.TryAddSingleton<IContextResolver, NullContextResolver>();
        services.TryAddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>();
        services.TryAddSingleton<MTokenInfo>();
        services.TryAddSingleton<GrpcRateLimiter>();
        services.TryAddSingleton<GrpcServerInterceptor>();

        global::Grpc.AspNetCore.Server.IGrpcServerBuilder grpcBuilder = services.AddGrpc(options =>
        {
            options.Interceptors.Add<GrpcServerInterceptor>();
            options.EnableDetailedErrors = grpcConfig.Server.EnableDetailedErrors;
            options.MaxSendMessageSize = grpcConfig.Server.MaxSendMessageSizeBytes;
            options.MaxReceiveMessageSize = grpcConfig.Server.MaxReceiveMessageSizeBytes;
            options.ResponseCompressionLevel = ParseCompressionLevel(grpcConfig.Server.ResponseCompressionLevel);
            options.ResponseCompressionAlgorithm = grpcConfig.Server.ResponseCompressionAlgorithm;
        });

        if (grpcConfig.Server.EnableJsonTranscoding)
        {
            _ = grpcBuilder.AddJsonTranscoding();
        }

        services.AddHealthChecks().AddCheck("grpc-runtime", () => HealthCheckResult.Healthy("gRPC runtime is configured."));
    }

    /// <summary>
    /// Registers a typed gRPC client using the supplied service URI.
    /// </summary>
    /// <typeparam name="TClient">The typed gRPC client type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceUri">The service URI.</param>
    /// <returns>The configured HTTP client builder.</returns>
    public static IHttpClientBuilder AddGrpcClient<TClient>(this IServiceCollection services, string serviceUri)
        where TClient : class
    {
        MGuard.NotNull(services);
        GrpcServiceConfig config = new()
        {
            Uri = serviceUri
        };
        return AddConfiguredGrpcClient<TClient>(services, config, new GrpcClientDefaultsConfig());
    }

    /// <summary>
    /// Registers multiple typed gRPC clients from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration source.</param>
    /// <param name="clients">The map of configuration key to typed client type.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddGrpcClients(
        this IServiceCollection services,
        IConfiguration configuration,
        Dictionary<string, Type> clients)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);
        _ = MGuard.NotNull(clients);

        GrpcServicesConfig grpcServicesConfig = GrpcServicesConfigBinding.Bind(configuration);
        services.TryAddSingleton(_ => Options.Create(grpcServicesConfig));

        foreach (KeyValuePair<string, Type> client in clients)
        {
            if (!grpcServicesConfig.Services!.TryGetValue(client.Key, out GrpcServiceConfig? serviceConfig) ||
                string.IsNullOrWhiteSpace(serviceConfig.Uri))
            {
                throw new MNotFoundException("GrpcClient", client.Key);
            }

            services.AddGrpcClientConfigured(client.Value, serviceConfig, grpcServicesConfig.ClientDefaults);
        }

        return services;
    }

    /// <summary>
    /// Enables transport-level gRPC middleware such as gRPC-Web.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configuration">The optional configuration source.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseGrpcTransport(this IApplicationBuilder app, IConfiguration? configuration = null)
    {
        MGuard.NotNull(app);
        if (configuration is null)
        {
            return app;
        }

        GrpcServicesConfig config = GrpcServicesConfigBinding.Bind(configuration);
        if (config.Server.EnableGrpcWeb)
        {
            GrpcWebOptions options = new()
            {
                DefaultEnabled = config.Server.EnableGrpcWebForAllServices
            };
            app.UseGrpcWeb(options);
        }

        return app;
    }

    private static IHttpClientBuilder AddConfiguredGrpcClient<TClient>(
        IServiceCollection services,
        GrpcServiceConfig serviceConfig,
        GrpcClientDefaultsConfig defaults)
        where TClient : class
    {
        _ = new Uri(serviceConfig.Uri);
        EnsureGrpcClientInfrastructure(services);
        services.Replace(ServiceDescriptor.Singleton(CreateForwardingOptions<TClient>(serviceConfig, defaults)));
        services.TryAddTransient<GrpcClientAuthInterceptor<TClient>>();

        return services
            .AddGrpcClient<TClient>(o => o.Address = new Uri(serviceConfig.Uri))
            .ConfigureChannel(options => ApplyClientChannelOptions(options, serviceConfig, defaults))
            .AddInterceptor<GrpcClientTelemetryInterceptor>(InterceptorScope.Client)
            .AddInterceptor<GrpcClientAuthInterceptor<TClient>>(InterceptorScope.Client);
    }

    private static IHttpClientBuilder AddGrpcClient(this IServiceCollection services, Type clientType,
        string serviceUri)
    {
        MGuard.NotNull(services);
        _ = new Uri(serviceUri);

        MethodInfo method = typeof(GrpcHandler)
            .GetMethod(nameof(AddGrpcClientGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(clientType);

        object? result = method.Invoke(null, [services, serviceUri, new GrpcClientDefaultsConfig(), null]);

        return result as IHttpClientBuilder
               ?? throw new MInternalException("Failed to create the gRPC client builder.");
    }

    private static IHttpClientBuilder AddGrpcClientConfigured(
        this IServiceCollection services,
        Type clientType,
        GrpcServiceConfig serviceConfig,
        GrpcClientDefaultsConfig defaults)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(serviceConfig);
        MGuard.NotNull(defaults);
        _ = new Uri(serviceConfig.Uri);

        MethodInfo method = typeof(GrpcHandler)
            .GetMethod(nameof(AddGrpcClientGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(clientType);

        object? result = method.Invoke(null, [services, serviceConfig.Uri, defaults, serviceConfig]);
        return result as IHttpClientBuilder
               ?? throw new MInternalException("Failed to create the gRPC client builder.");
    }

    private static IHttpClientBuilder AddGrpcClientGeneric<TClient>(
        IServiceCollection services,
        string serviceUri,
        GrpcClientDefaultsConfig defaults,
        GrpcServiceConfig? serviceConfig = null)
        where TClient : class
    {
        GrpcServiceConfig resolved = serviceConfig ?? new GrpcServiceConfig { Uri = serviceUri };
        return AddConfiguredGrpcClient<TClient>(services, resolved, defaults);
    }

    private static void EnsureGrpcClientInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.TryAddSingleton<GrpcClientTelemetryInterceptor>();
    }

    private static GrpcClientAuthForwardingOptions<TClient> CreateForwardingOptions<TClient>(
        GrpcServiceConfig serviceConfig,
        GrpcClientDefaultsConfig defaults)
        where TClient : class
    {
        return new GrpcClientAuthForwardingOptions<TClient>
        {
            ForwardAuthToken = serviceConfig.ForwardAuthToken ?? defaults.ForwardAuthToken,
            ForwardTenantId = serviceConfig.ForwardTenantId ?? defaults.ForwardTenantId
        };
    }

    private static void ApplyClientChannelOptions(
        GrpcChannelOptions options,
        GrpcServiceConfig serviceConfig,
        GrpcClientDefaultsConfig defaults)
    {
        int maxReceive = serviceConfig.MaxReceiveMessageSizeBytes ?? defaults.MaxReceiveMessageSizeBytes;
        int maxSend = serviceConfig.MaxSendMessageSizeBytes ?? defaults.MaxSendMessageSizeBytes;
        int timeoutSeconds = serviceConfig.TimeoutSeconds ?? defaults.TimeoutSeconds;
        int retryCount = serviceConfig.RetryCount ?? defaults.RetryCount;
        int initialBackoffSeconds = serviceConfig.InitialBackoffSeconds ?? defaults.InitialBackoffSeconds;
        int maxBackoffSeconds = serviceConfig.MaxBackoffSeconds ?? defaults.MaxBackoffSeconds;

        options.MaxReceiveMessageSize = maxReceive;
        options.MaxSendMessageSize = maxSend;
        options.ThrowOperationCanceledOnCancellation = true;
        options.ServiceConfig = BuildServiceConfig(
            timeoutSeconds,
            retryCount,
            initialBackoffSeconds,
            maxBackoffSeconds,
            serviceConfig.LoadBalancingPolicy ?? defaults.LoadBalancingPolicy);
    }

    private static ServiceConfig BuildServiceConfig(
        int timeoutSeconds,
        int retryCount,
        int initialBackoffSeconds,
        int maxBackoffSeconds,
        string? loadBalancingPolicy)
    {
        ServiceConfig config = new();
        config.LoadBalancingConfigs.Add(ResolveLoadBalancingConfig(loadBalancingPolicy));

        MethodConfig defaults = BuildMethodConfig(
            MethodName.Default,
            timeoutSeconds,
            retryCount,
            initialBackoffSeconds,
            maxBackoffSeconds);
        config.MethodConfigs.Add(defaults);

        return config;
    }

    private static MethodConfig BuildMethodConfig(
        MethodName methodName,
        int timeoutSeconds,
        int retryCount,
        int initialBackoffSeconds,
        int maxBackoffSeconds)
    {
        MethodConfig methodConfig = new();
        methodConfig.Names.Add(methodName);

        if (retryCount > 0)
        {
            methodConfig.RetryPolicy = new RetryPolicy
            {
                MaxAttempts = Math.Max(2, retryCount + 1),
                InitialBackoff = TimeSpan.FromSeconds(Math.Max(1, initialBackoffSeconds)),
                MaxBackoff = TimeSpan.FromSeconds(Math.Max(1, maxBackoffSeconds)),
                BackoffMultiplier = 2.0
            };
            methodConfig.RetryPolicy.RetryableStatusCodes.Add(StatusCode.Unavailable);
            methodConfig.RetryPolicy.RetryableStatusCodes.Add(StatusCode.DeadlineExceeded);
            methodConfig.RetryPolicy.RetryableStatusCodes.Add(StatusCode.ResourceExhausted);
        }

        return methodConfig;
    }

    private static LoadBalancingConfig ResolveLoadBalancingConfig(string? policy)
    {
        if (string.Equals(policy, "round_robin", StringComparison.OrdinalIgnoreCase))
        {
            return new RoundRobinConfig();
        }

        return new PickFirstConfig();
    }

    private static System.IO.Compression.CompressionLevel ParseCompressionLevel(string? value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out System.IO.Compression.CompressionLevel level))
        {
            return level;
        }

        return System.IO.Compression.CompressionLevel.Optimal;
    }
}
