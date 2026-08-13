using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Dependency injection helpers for ruleset storage and execution services.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public static class RuleEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRuleSetStore"/> backed by the filesystem and the <see cref="RulesEngineService"/>.
    /// Configure storage via the "RuleStore" configuration section.
    /// </summary>
    public static IServiceCollection AddRuleEngineStore(this IServiceCollection services, IConfiguration configuration)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.RuleEngine);

        RuleStoreConfigs configs = new();
        configuration.GetSection(RuleStoreConfigs.SectionName).Bind(configs);
        services.TryAddSingleton(configs);
        services.TryAddSingleton<IMemoryCache, MemoryCache>();

        services.TryAddSingleton<IRuleSetChangeNotifier>(sp =>
        {
            return new InMemoryRuleSetChangeNotifier();
        });

        services.TryAddSingleton<IRuleSetRuntimeCache, RuleSetRuntimeCache>();

        services.TryAddSingleton<IRuleSetStore>(sp =>
        {
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            string rootPath = ResolveRootPath(configs, env);
            IRuleSetSigner? signer = sp.GetService<IRuleSetSigner>();
            return new FileRuleSetStore(rootPath, signer, configs);
        });
        services.TryAddScoped<RulesEngineService>();

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
}
