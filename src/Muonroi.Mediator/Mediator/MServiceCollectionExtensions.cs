using Microsoft.Extensions.DependencyInjection;
using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Mediator.Context;
using Muonroi.Mediator.Mediator.Interfaces;
using System.Reflection;

namespace Muonroi.Mediator.Mediator;

/// <summary>
/// Configuration options for the Muonroi Mediator pipeline.
/// </summary>
public sealed class MMediatorOptions
{
    /// <summary>Default notification dispatch strategy. Default: Sequential.</summary>
    public MNotificationStrategy DefaultNotificationStrategy { get; set; } = MNotificationStrategy.Sequential;

    /// <summary>Assemblies to scan for handlers, processors, and exception handlers.</summary>
    public Assembly[] Assemblies { get; set; } = [];

    /// <summary>Internal list of pipeline behaviors to register.</summary>
    internal List<Type> Behaviors { get; } = [];

    /// <summary>
    /// Adds a pipeline behavior to the mediator.
    /// Behaviors are executed in the order they are added (Outer-to-Inner).
    /// </summary>
    public MMediatorOptions AddBehavior(Type behaviorType)
    {
        if (!Behaviors.Contains(behaviorType))
            Behaviors.Add(behaviorType);
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the mediator.
    /// </summary>
    public MMediatorOptions AddBehavior<TBehavior>()
    {
        return AddBehavior(typeof(TBehavior));
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MMediator with a configurable pipeline.
    /// </summary>
    public static IServiceCollection AddMMediator(
        this IServiceCollection services,
        Action<MMediatorOptions>? configure = null)
    {
        MMediatorOptions options = new();
        configure?.Invoke(options);

        // ── Core ─────────────────────────────────────────────────────────────────
        services.AddSingleton<IMediator, MMediator>();
        services.AddTransient<ServiceFactory>(sp => new ServiceFactory(sp.GetService));
        services.AddScoped<IRequestContextBag, MRequestContextBag>();

        // ── Scan assemblies ──────────────────────────────────────────────────────
        if (options.Assemblies is { Length: > 0 })
        {
            foreach (Assembly assembly in options.Assemblies)
            {
                foreach (TypeInfo type in assembly.DefinedTypes)
                {
                    if (type.IsAbstract || type.IsInterface) continue;

                    foreach (Type iface in type.ImplementedInterfaces)
                    {
                        if (!iface.IsGenericType) continue;
                        Type def = iface.GetGenericTypeDefinition();

                        if (def == typeof(IRequestHandler<,>) ||
                            def == typeof(INotificationHandler<>) ||
                            def == typeof(IStreamRequestHandler<,>) ||
                            def == typeof(IRequestPreProcessor<>) ||
                            def == typeof(IRequestPostProcessor<,>) ||
                            def == typeof(IRequestExceptionHandler<,,>))
                        {
                            if (type.IsGenericTypeDefinition &&
                                type.GetGenericArguments().Length != def.GetGenericArguments().Length)
                            {
                                continue;
                            }

                            services.AddTransient(type.IsGenericTypeDefinition ? def : iface, type.AsType());
                        }
                    }
                }
            }
        }

        // ── Pipeline behaviors — registered in REVERSE execution order ───────────
        // MMediator reverses the list, so register INNERMOST behaviors FIRST.
        // Outer behaviors are registered LAST.

        foreach (var behaviorType in options.Behaviors)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);
        }

        return services;
    }

    /// <summary>
    /// Registers standard ecosystem behaviors for Muonroi.
    /// Order (Outer-to-Inner): ExceptionHandler -> Logging -> Tenant -> Auth -> Validation -> Pre/Post
    /// </summary>
    public static MMediatorOptions AddMuonroiEcosystem(this MMediatorOptions options)
    {
        // Inner-most first (due to MMediator's reverse logic)
        options.AddBehavior(typeof(MPostProcessorBehavior<,>));
        options.AddBehavior(typeof(MPreProcessorBehavior<,>));
        options.AddBehavior(typeof(ValidationBehavior<,>));
        options.AddBehavior(typeof(MAuthorizationBehavior<,>));
        options.AddBehavior(typeof(MTenantValidationBehavior<,>));
        options.AddBehavior(typeof(LoggingBehavior<,>));
        options.AddBehavior(typeof(MExceptionHandlerBehavior<,>));
        return options;
    }

    /// <summary>
    /// Backward-compatible overload.
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[]? assemblies)
    {
        return services.AddMMediator(o =>
        {
            o.Assemblies = assemblies ?? [];
        });
    }
}
