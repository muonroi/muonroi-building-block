using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Logging;

namespace Muonroi.Core.Extensions;

public static class CoreServiceCollectionExtensions
{
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

    public static IServiceCollection AddRedisConfiguration(this IServiceCollection services,
        IConfiguration configuration,
        bool isSecretDefault = true,
        string secretKey = "")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
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
