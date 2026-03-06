namespace Muonroi.ServiceDiscovery.Consul.Consul;

public static class ConsulHandler
{
    public static IServiceCollection AddServiceDiscovery(this IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ConsulConfigs consulConfigs = new();
        configuration.GetSection(ConsulConfigs.SectionName).Bind(consulConfigs);
        services.TryAddSingleton(consulConfigs);

        if (!consulConfigs.Enable || !consulConfigs.UseDiscovery) return services;
        if (environment.IsDevelopment()) return services;

        if (string.IsNullOrEmpty(consulConfigs.ServiceName) ||
            string.IsNullOrEmpty(consulConfigs.ConsulAddress))
        {
            Console.WriteLine("Consul configuration is missing or incomplete. Service Discovery will be disabled.");
            return services;
        }

        return services.AddSingleton<IConsulClient, ConsulClient>(_ => new ConsulClient(config =>
        {
            config.Address = new Uri(consulConfigs.ConsulAddress);
        }));
    }

    public static IApplicationBuilder UseServiceDiscovery(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        return app.UseServiceDiscoveryAsync(environment).GetAwaiter().GetResult();
    }

    public static async Task<IApplicationBuilder> UseServiceDiscoveryAsync(this IApplicationBuilder app,
        IWebHostEnvironment environment)
    {
        ConsulConfigs? consulSettings = app.ApplicationServices.GetService<ConsulConfigs>();
        if (consulSettings is null) return app;
        if (!consulSettings.Enable || !consulSettings.UseDiscovery) return app;
        if (environment.IsDevelopment()) return app;

        if (string.IsNullOrEmpty(consulSettings.ServiceName) ||
            string.IsNullOrEmpty(consulSettings.ConsulAddress))
        {
            Console.WriteLine("Consul configuration is missing or incomplete. Service Discovery will be disabled.");
            return app;
        }

        IConsulClient? consulClient = app.ApplicationServices.GetService<IConsulClient>();
        if (consulClient is null)
        {
            Console.WriteLine("Consul client is not registered. Service Discovery will be disabled.");
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

        if (string.IsNullOrWhiteSpace(address) || port == 0)
            throw new InvalidOperationException("Service address or port could not be determined.");

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
