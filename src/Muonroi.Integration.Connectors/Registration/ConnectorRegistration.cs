using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Database;
using Muonroi.Integration.Connectors.Email;
using Muonroi.Integration.Connectors.Http;
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

        services.TryAddSingleton<IConnectorRegistry, DefaultConnectorRegistry>();

        return services;
    }
}
