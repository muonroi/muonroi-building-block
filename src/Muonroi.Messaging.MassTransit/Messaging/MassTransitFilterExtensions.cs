namespace Muonroi.Messaging.MassTransit.Messaging;

public static class MassTransitFilterExtensions
{
    public static void AddConsumeFilter(this IBusRegistrationConfigurator configurator, Type filterType)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(filterType);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) => { cfg.UseConsumeFilter(filterType, context); });
    }

    public static void AddPublishFilter(this IBusRegistrationConfigurator configurator, Type filterType)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(filterType);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) => { cfg.UsePublishFilter(filterType, context); });
    }

    public static void AddSendFilter(this IBusRegistrationConfigurator configurator, Type filterType)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(filterType);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) => { cfg.UseSendFilter(filterType, context); });
    }

    public static void ApplyRuntimePolicies(this IBusRegistrationConfigurator configurator, MessageBusRuntimeConfigs runtime)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(runtime);

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
