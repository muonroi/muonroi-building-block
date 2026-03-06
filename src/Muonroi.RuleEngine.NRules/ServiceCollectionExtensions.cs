using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.NRules;

/// <summary>DI helpers for registering the NRules engine.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds the NRules engine and loads rules from the given assemblies.</summary>
    public static IServiceCollection AddNRulesEngine(this IServiceCollection services, Action<RuleOptions>? configure,
        params Assembly[] assemblies)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton(provider =>
        {
            IOptions<RuleOptions> options = provider.GetRequiredService<IOptions<RuleOptions>>();
            return new NRulesEngine(assemblies, options);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, Contributors.NRulesManifestContributor>());

        return services;
    }

    /// <summary>Adds NRules HTTP endpoints as MVC application parts.</summary>
    public static IServiceCollection AddNRulesWeb(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(Controllers.NRulesController).Assembly);
        return services;
    }
}
