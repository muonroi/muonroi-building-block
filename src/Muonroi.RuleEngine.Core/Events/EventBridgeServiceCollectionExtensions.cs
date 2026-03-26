using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Extension methods for registering event bridge services in the DI container.
/// </summary>
public static class EventBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default rule event bridge services.
    /// Registers <see cref="InMemoryEventSink"/> as <see cref="IEventSink"/> (singleton).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRuleEventBridge(this IServiceCollection services)
    {
        services.AddSingleton<IEventSink, InMemoryEventSink>();
        return services;
    }

    /// <summary>
    /// Registers the default rule event bridge services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">A delegate to configure <see cref="WebhookOptions"/>.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRuleEventBridge(
        this IServiceCollection services,
        Action<WebhookOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IEventSink, InMemoryEventSink>();
        return services;
    }

    /// <summary>
    /// Registers the HTTP webhook notifier for rule events.
    /// Adds a named <see cref="System.Net.Http.HttpClient"/> called "RuleWebhook"
    /// and registers <see cref="IRuleWebhookNotifier"/> as <see cref="HttpRuleWebhookNotifier"/> (singleton).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">A delegate to configure <see cref="WebhookOptions"/>.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRuleWebhook(
        this IServiceCollection services,
        Action<WebhookOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient("RuleWebhook");
        services.AddSingleton<IRuleWebhookNotifier, HttpRuleWebhookNotifier>();
        return services;
    }
}
