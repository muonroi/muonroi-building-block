namespace Muonroi.ServiceDiscovery.Consul.Consul;

/// <summary>
/// Service discovery registration and application lifecycle hooks for Consul.
/// </summary>
public static class ConsulHandler
{
    private sealed class ConsulHandlerLogger { }

    /// <summary>
    /// Registers Consul service discovery components.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <param name="configuration">Configuration source.</param>
    /// <param name="environment">Hosting environment.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddServiceDiscovery(this IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ConsulConfigs consulConfigs = new();
        configuration.GetSection(ConsulConfigs.SectionName).Bind(consulConfigs);
        services.TryAddSingleton(consulConfigs);

        if (!consulConfigs.Enable || !consulConfigs.UseDiscovery)
        {
            return services;
        }

        if (environment.IsDevelopment())
        {
            return services;
        }

        if (string.IsNullOrEmpty(consulConfigs.ServiceName) ||
            string.IsNullOrEmpty(consulConfigs.ConsulAddress))
        {
            // Cannot use IMLog<T> here (DI container not yet built).
            // Returning silently — Consul client will simply not be registered.
            // The absence of IConsulClient in DI is the signal that discovery is disabled.
            return services;
        }

        return services.AddSingleton<IConsulClient, ConsulClient>(_ => new ConsulClient(config =>
        {
            config.Address = new Uri(consulConfigs.ConsulAddress);
        }));
    }

    /// <summary>
    /// Registers the service instance with Consul synchronously.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <param name="environment">Hosting environment.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseServiceDiscovery(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        return app.UseServiceDiscoveryAsync(environment).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Registers the service instance with Consul asynchronously.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <param name="environment">Hosting environment.</param>
    /// <returns>The application builder.</returns>
    public static async Task<IApplicationBuilder> UseServiceDiscoveryAsync(this IApplicationBuilder app,
        IWebHostEnvironment environment)
    {
        ConsulConfigs? consulSettings = app.ApplicationServices.GetService<ConsulConfigs>();
        if (consulSettings is null)
        {
            return app;
        }

        if (!consulSettings.Enable || !consulSettings.UseDiscovery)
        {
            return app;
        }

        if (environment.IsDevelopment())
        {
            return app;
        }

        IMLog<ConsulHandlerLogger> logger = app.ApplicationServices.GetRequiredService<IMLog<ConsulHandlerLogger>>();

        if (string.IsNullOrEmpty(consulSettings.ServiceName) ||
            string.IsNullOrEmpty(consulSettings.ConsulAddress))
        {
            logger.Warn("Consul configuration is missing or incomplete. Service Discovery will be disabled.");
            return app;
        }

        IConsulClient? consulClient = app.ApplicationServices.GetService<IConsulClient>();
        if (consulClient is null)
        {
            logger.Warn("Consul client is not registered. Service Discovery will be disabled.");
            return app;
        }

        IHostApplicationLifetime lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        string? address = consulSettings.ServiceAddress;
        int port = consulSettings.ServicePort;

        if (string.IsNullOrWhiteSpace(address))
        {
            IServerAddressesFeature? features = app.ServerFeatures.Get<IServerAddressesFeature>();
            string? firstAddress = features?.Addresses.FirstOrDefault();

            if (firstAddress != null)
            {
                Uri uri = new(firstAddress);
                address = uri.Host;
                port = uri.Port;
            }
        }

        MGuard.Configured(!(string.IsNullOrWhiteSpace(address) || port == 0), "Service address or port could not be determined.", "Consul:ServiceAddress");

        AgentServiceRegistration registration = new()
        {
            ID = $"{consulSettings.ServiceName}-{Guid.NewGuid()}",
            Name = consulSettings.ServiceName,
            Address = address,
            Port = port,
            Meta = consulSettings.ServiceMetadata
        };

        await consulClient.Agent.ServiceDeregister(registration.ID);
        await consulClient.Agent.ServiceRegister(registration);

        lifetime.ApplicationStopping.Register(() => { consulClient.Agent.ServiceDeregister(registration.ID).Wait(); });

        return app;
    }
}
