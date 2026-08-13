using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.RuleEngine.Core.Workflow;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.Core.Contributors;

namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Dependency injection helpers for the rule engine.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the rule orchestrator.</summary>
    public static IServiceCollection AddRuleEngine(this IServiceCollection services, Action<MRuleEngineOptions>? configure = null)
    {
        services.AddScoped(typeof(RuleOrchestrator<>));
        services.AddScoped(typeof(IRuleFactory<>), typeof(DefaultRuleFactory<>));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, RuleFlowManifestContributor>());
        if (configure != null)
        {
            services.Configure(configure);
        }
        return services;
    }

    /// <summary>
    /// Registers a context-specific rule engine pipeline with runtime mode routing support.
    /// </summary>
    public static MRuleEngineBuilder<TContext> AddMRuleEngine<TContext>(
        this IServiceCollection services,
        Action<MRuleEngineOptions>? configure = null)
    {
        services.GetOrCreateRegistry().Register(MCapability.RuleEngine);
        services.AddLogging();
        services.AddRuleEngine(configure);

        services.AddScoped<IMRuleExecutionRouter<TContext>, MRuleExecutionRouter<TContext>>();
        services.AddScoped<IMRuleWorkflowRunner<TContext>, MRuleWorkflowRunner<TContext>>();
        return new MRuleEngineBuilder<TContext>(services);
    }

    /// <summary>
    /// Configures workflow execution options for typed workflow runner.
    /// </summary>
    public static IServiceCollection ConfigureRuleWorkflow(
        this IServiceCollection services,
        Action<MRuleWorkflowOptions> configure)
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// Unified API alias for registering a typed rule engine pipeline.
    /// </summary>
    public static MRuleEngineBuilder<TContext> AddRuleEngine<TContext>(
        this IServiceCollection services,
        Action<MRuleEngineOptions>? configure = null)
    {
        return services.AddMRuleEngine<TContext>(configure);
    }

    /// <summary>
    /// Legacy alias kept for backward compatibility.
    /// </summary>
    [Obsolete("Use AddRuleEngine<TContext>(...) or AddMRuleEngine<TContext>(...) instead.")]
    public static MRuleEngineBuilder<TContext> AddRuleOrchestrator<TContext>(
        this IServiceCollection services,
        Action<MRuleEngineOptions>? configure = null)
    {
        return services.AddMRuleEngine<TContext>(configure);
    }

    /// <summary>Scans assemblies and registers rules and hook handlers.</summary>
    public static IServiceCollection AddRulesFromAssemblies(this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(c => c.AssignableTo(typeof(IRule<>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IHookHandler<>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        HashSet<string> keys = [];
        foreach (TypeInfo? type in assemblies.SelectMany(a => a.DefinedTypes))
        {
            foreach (RuleGroupAttribute group in type.GetCustomAttributes<RuleGroupAttribute>())
            {
                keys.Add(group.Key);
                RegisterKeyed(type, group.Key);
            }

            foreach (TenantRuleGroupAttribute group in type.GetCustomAttributes<TenantRuleGroupAttribute>())
            {
                keys.Add(group.Key);
                RegisterKeyed(type, group.Key);
            }
        }

        foreach (string key in keys) services.AddKeyedScoped(typeof(RuleOrchestrator<>), key, typeof(RuleOrchestrator<>));

        return services;

        void RegisterKeyed(Type type, string key)
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;

                Type def = iface.GetGenericTypeDefinition();
                if (def == typeof(IRule<>) || def == typeof(IHookHandler<>)) services.AddKeyedScoped(iface, key, type);
            }
        }
    }
}
