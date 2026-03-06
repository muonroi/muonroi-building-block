namespace Muonroi.AspNetCore.Extensions;

public static class PermissionProviderExtensions
{
    public static IServiceCollection AddPermissionProviders(this IServiceCollection services,
        params Assembly[] assemblies)
    {
        assemblies = assemblies.Length == 0 ? [Assembly.GetExecutingAssembly()] : assemblies;

        IEnumerable<Type> providerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IPermissionProvider).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        foreach (Type? type in providerTypes)
        {
            services.AddTransient(typeof(IPermissionProvider), type);
        }

        return services;
    }
}
