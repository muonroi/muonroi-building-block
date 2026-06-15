using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Database;
using Muonroi.Integration.Connectors.Email;
using Muonroi.Integration.Connectors.Http;
using Muonroi.Integration.Connectors.Presets;
using Muonroi.Integration.Connectors.Redis;
using Muonroi.Integration.Connectors.Slack;

namespace Muonroi.Integration.Connectors.Registration;

/// <summary>
/// DI extension for registering built-in connectors and the default connector registry.
/// </summary>
public static class ConnectorRegistration
{
    /// <summary>
    /// Registers all built-in connectors and the <see cref="DefaultConnectorRegistry"/>.
    /// </summary>
    public static IServiceCollection AddMBuiltInConnectors(this IServiceCollection services)
    {
        services.AddHttpClient("MuonroiConnector");

        services.TryAddSingleton<IServiceTaskConnector, HttpConnector>();
        services.AddSingleton<IServiceTaskConnector, SmtpConnector>();
        services.AddSingleton<IServiceTaskConnector, SlackWebhookConnector>();
        services.AddSingleton<IServiceTaskConnector, SqlQueryConnector>();
        services.AddSingleton<IServiceTaskConnector, RedisConnector>();

        // Phase 18 preset connectors (SRC-01) — delegate 100% to HttpConnector.
        // TryAddSingleton<HttpConnector> ensures the concrete type is resolvable for preset injection.
        services.TryAddSingleton<HttpConnector>();
        services.AddSingleton<IServiceTaskConnector, JiraCloudPresetConnector>();
        services.AddSingleton<IServiceTaskConnector, ConfluencePresetConnector>();
        services.AddSingleton<IServiceTaskConnector, GenericRestPresetConnector>();

        services.TryAddSingleton<IConnectorRegistry, DefaultConnectorRegistry>();

        return services;
    }
}
