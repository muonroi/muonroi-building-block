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

        // Phase 22 preset connectors (CONN-01) — 5 new paste-token auth presets.
        services.AddSingleton<IServiceTaskConnector, JiraServerPresetConnector>();
        services.AddSingleton<IServiceTaskConnector, ConfluenceServerPresetConnector>();
        services.AddSingleton<IServiceTaskConnector, AzureDevOpsPresetConnector>();
        services.AddSingleton<IServiceTaskConnector, NotionPresetConnector>();
        services.AddSingleton<IServiceTaskConnector, GitHubPresetConnector>();

        services.TryAddSingleton<IConnectorRegistry, DefaultConnectorRegistry>();

        return services;
    }
}
