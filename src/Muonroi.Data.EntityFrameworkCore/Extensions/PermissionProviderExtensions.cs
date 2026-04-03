namespace Muonroi.Data.EntityFrameworkCore.Extensions;

/// <summary>
/// Registers permission providers from assemblies.
/// </summary>
public static class PermissionProviderExtensions
{
    /// <summary>
    /// Adds all <see cref="IPermissionProvider"/> implementations from the given assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan. Defaults to the current assembly.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPermissionProviders(this IServiceCollection services, params Assembly[] assemblies)
    {
        assemblies = assemblies.Length == 0 ? [Assembly.GetExecutingAssembly()] : assemblies;

        IEnumerable<Type> providerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IPermissionProvider).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        foreach (Type type in providerTypes)
        {
            services.AddTransient(typeof(IPermissionProvider), type);
        }

        return services;
    }
}
