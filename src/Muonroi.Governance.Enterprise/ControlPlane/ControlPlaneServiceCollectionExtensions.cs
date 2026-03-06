namespace Muonroi.Governance.ControlPlane;

public static class MControlPlaneServiceCollectionExtensions
{
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


