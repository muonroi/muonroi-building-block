using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Abstractions.License;
using Muonroi.RuleEngine.Core.Events;
using Muonroi.RuleEngine.Runtime.Events;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Dependency injection helpers for ruleset storage and execution services.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "DI service resolution values are guaranteed non-null by DI container; null-forgiving is structurally correct.")]
public static class RuleEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRuleSetStore"/> backed by the filesystem and the <see cref="RulesEngineService"/>.
    /// Configure storage via the "RuleStore" and "RuleControlPlane" configuration sections.
    /// </summary>
    public static IServiceCollection AddRuleEngineStore(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RuleStoreConfigs>? configure = null,
        Action<RuleControlPlaneOptions>? configureControlPlane = null)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);
        // File-backed store is available in all tiers (Free, Licensed, Enterprise).

        services.Configure<RuleStoreConfigs>(configuration.GetSection(RuleStoreConfigs.SectionName));
        services.Configure<RuleControlPlaneOptions>(configuration.GetSection(RuleControlPlaneOptions.SectionName));

        if (configure is not null)
        {
            services.Configure(configure);
        }

        if (configureControlPlane is not null)
        {
            services.Configure(configureControlPlane);
        }

        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.TryAddSingleton<IRuleSetDefinitionValidator, RuleSetDefinitionValidator>();
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();

        // Type-safe context registry — replaces unsafe Type.GetType() + Deserialize(json, type) pattern
        services.TryAddSingleton(sp =>
        {
            IMEcosystemRegistry? ecosystemRegistry = sp.GetService<IMEcosystemRegistry>();
            return new MRuleContextJsonRegistry(ecosystemRegistry);
        });

        // Graph parser and context adapters for flow graph execution
        services.TryAddSingleton<RuleGraphParser>();
        services.TryAddSingleton(typeof(IContextProjector<>), typeof(ReflectionContextProjector<>));
        services.TryAddSingleton(typeof(IContextFactory<>), typeof(ReflectionContextFactory<>));

        services.TryAddSingleton<IRuleSetChangeNotifier>(sp =>
        {
            return new InMemoryRuleSetChangeNotifier();
        });

        services.TryAddSingleton<IRuleSetRuntimeCache>(sp =>
        {
            RuleStoreConfigs cfg = sp.GetRequiredService<IOptions<RuleStoreConfigs>>().Value;
            if (cfg.EnableRuntimeCache)
            {
                return new RuleSetRuntimeCache(
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<IOptions<RuleStoreConfigs>>(),
                    sp.GetService<IRuleSetChangeNotifier>());
            }
            return null!;
        });

        services.TryAddSingleton<IRuleSetStore>(sp =>
        {
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            RuleStoreConfigs cfg = sp.GetRequiredService<IOptions<RuleStoreConfigs>>().Value;
            string rootPath = ResolveRootPath(cfg, env);
            IRuleSetSigner? signer = sp.GetService<IRuleSetSigner>();
            ISystemExecutionContextAccessor executionContextAccessor = sp.GetRequiredService<ISystemExecutionContextAccessor>();
            return new FileRuleSetStore(rootPath, signer, sp.GetRequiredService<IOptions<RuleStoreConfigs>>(), executionContextAccessor);
        });
        services.TryAddSingleton<IRuleSetAuditStore>(sp =>
        {
            IHostEnvironment? env = sp.GetService<IHostEnvironment>();
            RuleStoreConfigs cfg = sp.GetRequiredService<IOptions<RuleStoreConfigs>>().Value;
            string rootPath = ResolveRootPath(cfg, env);
            IMJsonSerializeService serializer = sp.GetRequiredService<IMJsonSerializeService>();
            ISystemExecutionContextAccessor executionContextAccessor = sp.GetRequiredService<ISystemExecutionContextAccessor>();
            return new FileRuleSetAuditStore(rootPath, serializer, executionContextAccessor);
        });
        services.TryAddScoped<RulesEngineService>();

        return services;
    }



    /// <summary>
    /// Wires the CloudEvents bridge into the rule engine pipeline.
    /// <list type="bullet">
    ///   <item>Decorates the existing <see cref="IRuleSetChangeNotifier"/> registration with
    ///   <see cref="CloudEventPublishingNotifier"/> so that every ruleset lifecycle change also
    ///   publishes a CloudEvent to <see cref="IEventSink"/>.</item>
    ///   <item>Registers <see cref="InMemoryEventSink"/> as <see cref="IEventSink"/> (singleton) if
    ///   no <see cref="IEventSink"/> is already registered.</item>
    ///   <item>Registers <see cref="RuleExecutionEventPublisher"/> as a singleton.</item>
    ///   <item>Registers <see cref="EventDrivenRuleEvaluator"/> as scoped (needs tenant context per request).</item>
    /// </list>
    /// Call this method after <see cref="AddRuleEngineStore"/>.
    /// </summary>
    public static IServiceCollection AddRuleEventBridge(this IServiceCollection services)
        => services.AddRuleEngineEventBridge();

    /// <summary>
    /// Wires the CloudEvents bridge into the rule engine pipeline.
    /// <list type="bullet">
    ///   <item>Decorates the existing <see cref="IRuleSetChangeNotifier"/> with <see cref="CloudEventPublishingNotifier"/>.</item>
    ///   <item>Registers <see cref="InMemoryEventSink"/> as <see cref="IEventSink"/> if none registered.</item>
    ///   <item>Registers <see cref="RuleExecutionEventPublisher"/> (singleton) and <see cref="EventDrivenRuleEvaluator"/> (scoped).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddRuleEngineEventBridge(this IServiceCollection services)
    {
        MGuard.NotNull(services);

        // Register InMemoryEventSink as default IEventSink if none registered
        services.TryAddSingleton<IEventSink, InMemoryEventSink>();

        // Decorate the existing IRuleSetChangeNotifier with CloudEventPublishingNotifier
        // We do this by replacing the registration with a factory that wraps the original.
        ServiceDescriptor? existingNotifier = services
            .LastOrDefault(sd => sd.ServiceType == typeof(IRuleSetChangeNotifier));

        if (existingNotifier is not null)
        {
            // Remove the existing registration
            services.Remove(existingNotifier);

            // Re-register as a decorator factory
            services.AddSingleton<IRuleSetChangeNotifier>(sp =>
            {
                // Recreate the original notifier using the descriptor
                IRuleSetChangeNotifier inner = existingNotifier.ImplementationInstance as IRuleSetChangeNotifier
                    ?? (existingNotifier.ImplementationFactory is not null
                        ? (IRuleSetChangeNotifier)existingNotifier.ImplementationFactory(sp)
                        : (IRuleSetChangeNotifier)ActivatorUtilities.CreateInstance(sp, existingNotifier.ImplementationType!));

                IEventSink eventSink = sp.GetRequiredService<IEventSink>();
                return new CloudEventPublishingNotifier(inner, eventSink);
            });
        }
        else
        {
            // No existing notifier — register InMemoryRuleSetChangeNotifier wrapped in the decorator
            services.AddSingleton<IRuleSetChangeNotifier>(sp =>
            {
                InMemoryRuleSetChangeNotifier inner = new();
                IEventSink eventSink = sp.GetRequiredService<IEventSink>();
                return new CloudEventPublishingNotifier(inner, eventSink);
            });
        }

        // Register the execution event publisher
        services.TryAddSingleton<RuleExecutionEventPublisher>();

        // Register the event-driven evaluator as scoped (requires tenant context)
        services.TryAddScoped<IEventDrivenRuleEvaluator, EventDrivenRuleEvaluator>();
        services.TryAddScoped<EventDrivenRuleEvaluator>();

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
