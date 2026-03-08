using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Dependency injection helpers for ruleset storage and execution services.
/// </summary>
public static class RuleEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRuleSetStore"/> backed by the filesystem and the <see cref="RulesEngineService"/>.
    /// Configure storage via the "RuleStore" and "RuleControlPlane" configuration sections.
    /// </summary>
    public static IServiceCollection AddRuleEngineStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        // File-backed store is available in all tiers (Free, Licensed, Enterprise).
        // EnsureFeatureOrThrow is reserved for Postgres/Redis-backed stores (AddMRuleEngineWithPostgres).

        RuleStoreConfigs storeConfigs = new();
        configuration.GetSection(RuleStoreConfigs.SectionName).Bind(storeConfigs);

        RuleControlPlaneOptions controlPlaneOptions = new();
        configuration.GetSection(RuleControlPlaneOptions.SectionName).Bind(controlPlaneOptions);
        if (storeConfigs.RequireApproval)
        {
            controlPlaneOptions.RequireApproval = true;
        }

        if (!storeConfigs.NotifyOnStateChange)
        {
            controlPlaneOptions.NotifyOnStateChange = false;
        }

        ReplaceSingleton(services, storeConfigs);
        ReplaceSingleton(services, controlPlaneOptions);
        services.TryAddSingleton<IRuleSetDefinitionValidator, RuleSetDefinitionValidator>();
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();

        services.TryAddSingleton<IRuleSetChangeNotifier>(sp =>
        {
            IConnectionMultiplexer? redis = sp.GetService<IConnectionMultiplexer>();
            RuleStoreConfigs cfg = sp.GetRequiredService<RuleStoreConfigs>();
            IMJsonSerializeService serializer = sp.GetRequiredService<IMJsonSerializeService>();
            if (redis is not null)
            {
                return new RedisRuleSetChangeNotifier(redis, cfg.RuleChangeChannel, serializer);
            }

            return new InMemoryRuleSetChangeNotifier();
        });

        if (storeConfigs.EnableRuntimeCache)
        {
            services.TryAddSingleton<IRuleSetRuntimeCache, RuleSetRuntimeCache>();
        }

        services.TryAddSingleton<IRuleSetStore>(sp =>
        {
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            string rootPath = ResolveRootPath(storeConfigs, env);
            IRuleSetSigner? signer = sp.GetService<IRuleSetSigner>();
            return new FileRuleSetStore(rootPath, signer, storeConfigs);
        });
        services.TryAddSingleton<IRuleSetAuditStore>(sp =>
        {
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            string rootPath = ResolveRootPath(storeConfigs, env);
            IMJsonSerializeService serializer = sp.GetRequiredService<IMJsonSerializeService>();
            return new FileRuleSetAuditStore(rootPath, serializer);
        });
        services.TryAddScoped<RulesEngineService>();

        return services;
    }

    /// <summary>
    /// Registers Postgres-backed ruleset storage, audit, approval, and canary services.
    /// </summary>
    public static IServiceCollection AddMRuleEngineWithPostgres(
        this IServiceCollection services,
        string connectionString,
        Action<RuleControlPlaneOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.RuleEngine);

        RuleStoreConfigs storeConfigs = GetOrCreateRuleStoreConfigs(services);
        RuleControlPlaneOptions controlPlaneOptions = GetOrCreateControlPlaneOptions(services);
        configureOptions?.Invoke(controlPlaneOptions);
        ReplaceSingleton(services, storeConfigs);
        ReplaceSingleton(services, controlPlaneOptions);

        services.TryAddSingleton<IRuleSetDefinitionValidator, RuleSetDefinitionValidator>();
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        services.TryAddSingleton<IRuleSetAuditSigner>(_ => CreateAuditSigner(controlPlaneOptions));
        services.TryAddSingleton<IRuleSetChangeNotifier, InMemoryRuleSetChangeNotifier>();

        services.AddDbContext<RuleEngineDbContext>(options => options.UseNpgsql(connectionString));

        services.Replace(ServiceDescriptor.Scoped<IRuleSetStore, PostgresRuleSetStore>());
        services.Replace(ServiceDescriptor.Scoped<IRuleSetAuditStore, PostgresRuleSetAuditStore>());
        services.TryAddScoped<IRuleSetApprovalService, RuleSetApprovalService>();
        services.TryAddScoped<ICanaryRolloutService, CanaryRolloutService>();
        services.TryAddScoped<RulesEngineService>();

        return services;
    }

    /// <summary>
    /// Registers Redis pub/sub notifier for cross-node hot reload.
    /// </summary>
    public static IServiceCollection AddMRuleEngineWithRedisHotReload(
        this IServiceCollection services,
        string redisConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);

        RuleStoreConfigs configs = GetOrCreateRuleStoreConfigs(services);
        ReplaceSingleton(services, configs);
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.Replace(ServiceDescriptor.Singleton<IRuleSetChangeNotifier>(sp =>
            new RedisRuleSetChangeNotifier(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                configs.RuleChangeChannel,
                sp.GetRequiredService<IMJsonSerializeService>())));
        return services;
    }

    /// <summary>
    /// Enables maker-checker approval workflow for rulesets.
    /// </summary>
    public static IServiceCollection AddMRuleEngineApprovalWorkflow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        RuleControlPlaneOptions options = GetOrCreateControlPlaneOptions(services);
        options.RequireApproval = true;
        ReplaceSingleton(services, options);
        services.TryAddScoped<IRuleSetApprovalService, RuleSetApprovalService>();
        return services;
    }

    /// <summary>
    /// Enables tenant-targeted canary rollout services.
    /// </summary>
    public static IServiceCollection AddMCanaryRollout(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        RuleControlPlaneOptions options = GetOrCreateControlPlaneOptions(services);
        options.EnableCanary = true;
        ReplaceSingleton(services, options);
        services.TryAddScoped<ICanaryRolloutService, CanaryRolloutService>();
        return services;
    }

    private static string ResolveRootPath(RuleStoreConfigs configs, IHostEnvironment? environment)
    {
        string basePath = configs.UseContentRoot && !string.IsNullOrWhiteSpace(environment?.ContentRootPath)
            ? environment.ContentRootPath
            : AppContext.BaseDirectory;

        string? rootPath = configs.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = Path.Combine(basePath, "rules");
        }
        else if (!Path.IsPathRooted(rootPath))
        {
            rootPath = Path.Combine(basePath, rootPath);
        }

        return Path.GetFullPath(rootPath);
    }

    private static IRuleSetAuditSigner CreateAuditSigner(RuleControlPlaneOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AuditPrivateKeyPem))
        {
            return RsaRuleSetAuditSigner.FromPrivateKeyPem(options.AuditPrivateKeyPem, options.AuditSignerKeyId);
        }

        if (!string.IsNullOrWhiteSpace(options.AuditPrivateKeyPemPath))
        {
            return RsaRuleSetAuditSigner.FromPrivateKeyFile(options.AuditPrivateKeyPemPath, options.AuditSignerKeyId);
        }

        return RsaRuleSetAuditSigner.CreateEphemeral(options.AuditSignerKeyId);
    }

    private static RuleStoreConfigs GetOrCreateRuleStoreConfigs(IServiceCollection services)
    {
        RuleStoreConfigs? existing = services
            .LastOrDefault(x => x.ServiceType == typeof(RuleStoreConfigs))
            ?.ImplementationInstance as RuleStoreConfigs;
        return existing ?? new RuleStoreConfigs();
    }

    private static RuleControlPlaneOptions GetOrCreateControlPlaneOptions(IServiceCollection services)
    {
        RuleControlPlaneOptions? existing = services
            .LastOrDefault(x => x.ServiceType == typeof(RuleControlPlaneOptions))
            ?.ImplementationInstance as RuleControlPlaneOptions;
        return existing ?? new RuleControlPlaneOptions();
    }

    private static void ReplaceSingleton<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                services.RemoveAt(i);
            }
        }

        services.AddSingleton(instance);
    }
}

