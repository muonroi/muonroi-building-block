namespace Muonroi.Governance.Authorization;

/// <summary>
/// Represents the Policy Decision Service Collection Extensions.
/// </summary>
public static class PolicyDecisionServiceCollectionExtensions
{
    /// <summary>
    /// Executes the Add MPolicy Decision operation.
    /// </summary>
    public static IServiceCollection AddMPolicyDecision(this IServiceCollection services, IConfiguration configuration)
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);

        MPolicyDecisionConfigs configs = new();
        configuration.GetSection(MPolicyDecisionConfigs.SectionName).Bind(configs);
        services.TryAddSingleton(configs);

        services.AddHttpClient("MuonroiPolicyDecision", client =>
        {
            if (Uri.TryCreate(configs.Endpoint, UriKind.Absolute, out Uri? endpoint))
            {
                client.BaseAddress = endpoint;
            }

            int timeout = configs.TimeoutSeconds > 0 ? configs.TimeoutSeconds : 5;
            client.Timeout = TimeSpan.FromSeconds(timeout);

            foreach (KeyValuePair<string, string> kvp in configs.DefaultHeaders)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                {
                    continue;
                }

                if (!client.DefaultRequestHeaders.Contains(kvp.Key))
                {
                    client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
                }
            }
        });

        services.TryAddSingleton<IMPolicyDecisionService, MPolicyDecisionService>();
        return services;
    }
}
