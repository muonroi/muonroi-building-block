namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for setting up core services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds core services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration to use for setting up services.</param>
    /// <param name="isSecretDefault">A flag indicating if secrets are default.</param>
    /// <param name="secretKey">The secret key for decryption.</param>
    /// <param name="paginationConfigs">Optional pagination configurations.</param>
    /// <param name="tokenConfig">Optional token information.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isSecretDefault,
        string secretKey,
        MPaginationConfig? paginationConfigs,
        MTokenInfo? tokenConfig)
    {
        _ = services.AddLogging(builder => builder.AddMuonroiLogging());
        _ = services.AddHttpContextAccessor()
            .AddProblemDetails()
            .AddSingleton<IMJsonSerializeService, MJsonSerializeService>()
            .AddSingleton<IMDateTimeService, MDateTimeService>()
            .AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>()
            .AddSingleton<IContextResolver, NullContextResolver>()
            .AddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>()
            .AddRedisConfiguration(configuration, isSecretDefault, secretKey)
            .AddPaginationConfigs(configuration, paginationConfigs)
            .AddJwtConfigs(configuration, tokenConfig)
            .AddSystemConfig(configuration);
        return services;
    }

    /// <summary>
    /// Adds Redis configuration to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="configuration">The configuration containing Redis settings.</param>
    /// <param name="isSecretDefault">A flag indicating if secrets are default (defaults to true).</param>
    /// <param name="secretKey">The secret key for decryption (defaults to empty string).</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisConfiguration(this IServiceCollection services,
        IConfiguration configuration,
        bool isSecretDefault = true,
        string secretKey = "")
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);
        RedisConfigs redisConfigs = new();
        configuration.GetSection(redisConfigs.SectionName).Bind(redisConfigs);

        string projectSeed = configuration.GetValue<string>("LicenseConfigs:ProjectSeed") ?? string.Empty;

        redisConfigs.Host =
            MStringExtension.DecryptConfigurationValue(configuration, redisConfigs.Host, isSecretDefault, secretKey, projectSeed) ??
            string.Empty;
        redisConfigs.Port =
            MStringExtension.DecryptConfigurationValue(configuration, redisConfigs.Port, isSecretDefault, secretKey, projectSeed) ??
            string.Empty;
        redisConfigs.Password =
            MStringExtension.DecryptConfigurationValue(configuration, redisConfigs.Password, isSecretDefault,
                secretKey, projectSeed) ?? string.Empty;
        redisConfigs.KeyPrefix =
            MStringExtension.DecryptConfigurationValue(configuration, redisConfigs.KeyPrefix, isSecretDefault,
                secretKey, projectSeed) ?? string.Empty;
        _ = services.AddSingleton(redisConfigs);
        return services;
    }

    private static IServiceCollection AddPaginationConfigs(this IServiceCollection services, IConfiguration configuration, MPaginationConfig? configs = null)
    {
        configs ??= new MPaginationConfig();
        configuration.GetSection(configs.SectionName).Bind(configs);
        _ = services.AddSingleton(configs);
        return services;
    }

    private static IServiceCollection AddJwtConfigs(this IServiceCollection services, IConfiguration configuration, MTokenInfo? configs = null)
    {
        configs ??= new MTokenInfo();
        configuration.GetSection(configs.SectionName).Bind(configs);
        _ = services.AddSingleton(configs);
        return services;
    }
}
