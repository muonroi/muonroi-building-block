namespace Muonroi.RuleEngine.Runtime.Web.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Runtime.Web.HotReload;

/// <summary>
/// Service registration helpers for ruleset hot-reload client.
/// </summary>
public static class RuleSetHotReloadExtensions
{
    /// <summary>
    /// Enables SignalR-based ruleset hot-reload from Control Plane.
    /// The consumer must separately register an <see cref="IRuleSetChangeHandlerClient"/>
    /// implementation to receive and act on ruleset change events.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Configuration action for <see cref="RuleSetHotReloadOptions"/>.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRuleSetHotReload(
        this IServiceCollection services,
        Action<RuleSetHotReloadOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        RuleSetHotReloadOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddHostedService<RuleSetHotReloadClient>();

        return services;
    }
}
