namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the MControl Plane Service Collection Extensions.
/// </summary>
public static class MControlPlaneServiceCollectionExtensions
{
    /// <summary>
    /// Executes the Add MEnterprise Control Plane operation.
    /// </summary>
    public static IServiceCollection AddMEnterpriseControlPlane(
        this IServiceCollection services,
        string registryPath,
        IMControlPlaneSigner signer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(signer);

        services.AddSingleton<IMControlPlaneStore>(sp =>
            new MFileControlPlaneStore(registryPath, sp.GetRequiredService<IMJsonSerializeService>()));
        services.AddSingleton(signer);
        services.AddSingleton<IMEnterpriseControlPlaneService, MEnterpriseControlPlaneService>();
        return services;
    }
}


