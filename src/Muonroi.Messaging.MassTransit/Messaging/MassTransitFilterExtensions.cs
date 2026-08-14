namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Mass Transit Filter Extensions.
/// </summary>
public static class MassTransitFilterExtensions
{
    /// <summary>
    /// Registers a message router in the service collection for rule-based routing.
    /// </summary>
    /// <typeparam name="TMessage">The message type handled by the router.</typeparam>
    /// <typeparam name="TRouter">The router implementation type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMessageRouter<TMessage, TRouter>(this IServiceCollection services)
        where TMessage : class
        where TRouter : class, IMessageRouter<TMessage>
    {
        MGuard.NotNull(services);
        services.TryAddScoped<TRouter>();
        services.TryAddScoped<IMessageRouter<TMessage>>(sp => sp.GetRequiredService<TRouter>());
        return services;
    }

    /// <summary>
    /// Executes the Add Consume Filter operation.
    /// </summary>
    public static void AddConsumeFilter(this IBusRegistrationConfigurator configurator, Type filterType)
    {
        MGuard.NotNull(configurator);
        MGuard.NotNull(filterType);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) => { cfg.UseConsumeFilter(filterType, context); });
    }

    /// <summary>
    /// Executes the Add Publish Filter operation.
    /// </summary>
    public static void AddPublishFilter(this IBusRegistrationConfigurator configurator, Type filterType)
    {
        MGuard.NotNull(configurator);
        MGuard.NotNull(filterType);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) => { cfg.UsePublishFilter(filterType, context); });
    }

    /// <summary>
    /// Executes the Add Send Filter operation.
    /// </summary>
    public static void AddSendFilter(this IBusRegistrationConfigurator configurator, Type filterType)
    {
        MGuard.NotNull(configurator);
        MGuard.NotNull(filterType);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) => { cfg.UseSendFilter(filterType, context); });
    }

    /// <summary>
    /// Enables the Muonroi inbox pattern for consumer deduplication.
    /// </summary>
    public static void UseMuonroiInbox(this IConsumePipeConfigurator configurator, IRegistrationContext context)
    {
        configurator.UseConsumeFilter(typeof(MuonroiInboxFilter<,>), context);
    }

    /// <summary>
    /// Executes the Apply Runtime Policies operation.
    /// </summary>
    public static void ApplyRuntimePolicies(this IBusRegistrationConfigurator configurator, MessageBusRuntimeConfigs runtime)
    {
        MGuard.NotNull(configurator);
        MGuard.NotNull(runtime);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) =>
        {
            if (runtime.PrefetchCount > 0)
            {
                cfg.PrefetchCount = (ushort)Math.Min(runtime.PrefetchCount, ushort.MaxValue);
            }

            if (runtime.ConcurrentMessageLimit > 0)
            {
                cfg.UseConcurrencyLimit(runtime.ConcurrentMessageLimit);
            }

            if (runtime.RetryCount > 0)
            {
                cfg.UseMessageRetry(r => r.Interval(
                    runtime.RetryCount,
                    TimeSpan.FromMilliseconds(Math.Max(10, runtime.RetryIntervalMs))));
            }

            if (runtime.EnableInMemoryOutbox)
            {
                cfg.UseInMemoryOutbox(context);
            }
        });
    }
}
