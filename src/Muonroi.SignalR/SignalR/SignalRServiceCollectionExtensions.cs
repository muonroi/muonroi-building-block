namespace Muonroi.SignalR.SignalR;

/// <summary>
/// Extension methods for registering SignalR with optional tenant support.
/// </summary>
public static class SignalRServiceCollectionExtensions
{
    /// <summary>
    /// Add SignalR and configure tenant filter when multi-tenant is enabled.
    /// </summary>
    public static IServiceCollection AddSignalRWithTenant(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddSignalR();

        Tenancy.Core.Legacy.MultiTenantConfigs options = new();
        configuration.GetSection(Tenancy.Core.Legacy.MultiTenantConfigs.SectionName).Bind(options);

        if (options.Enabled)
        {
            services.AddScoped<IHubFilter, TenantHubFilter>();
        }

        return services;
    }
}
