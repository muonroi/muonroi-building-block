using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Resolves and registers the configured background job provider.
/// </summary>
public static class BackgroundJobHandler
{
    /// <summary>
    /// Adds background job services based on configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BackgroundJobConfigs cfg = new();
        configuration.GetSection(BackgroundJobConfigs.SectionName).Bind(cfg);

        return cfg.JobType switch
        {
            JobType.Hangfire => InvokeProviderHandler(
                services,
                configuration,
                typeName: "Muonroi.BackgroundJobs.Hangfire.Hangfire.BackgroundJobHandler, Muonroi.BackgroundJobs.Hangfire"),
            JobType.Quartz => InvokeProviderHandler(
                services,
                configuration,
                typeName: "Muonroi.BackgroundJobs.Quartz.Quartz.BackgroundJobHandler, Muonroi.BackgroundJobs.Quartz"),
            _ => throw new MInternalException("Unsupported job type")
        };
    }

    private static IServiceCollection InvokeProviderHandler(
        IServiceCollection services,
        IConfiguration configuration,
        string typeName)
    {
        Type providerType = Type.GetType(typeName, throwOnError: true)!;
        MethodInfo? addMethod = providerType.GetMethod(
            name: "AddBackgroundJobs",
            bindingAttr: BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(IServiceCollection), typeof(IConfiguration)],
            modifiers: null);
        if (addMethod == null)
        {
            throw new MissingMethodException(providerType.FullName, "AddBackgroundJobs(IServiceCollection, IConfiguration)");
        }

        object? result = addMethod.Invoke(null, [services, configuration]);
        return result as IServiceCollection ?? services;
    }
}
