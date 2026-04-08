using Microsoft.Extensions.DependencyInjection;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.File;
using Muonroi.Experience.Runtime.Qdrant;
using Qdrant.Client;

namespace Muonroi.Experience.Runtime;

/// <summary>
/// DI registration extensions for the Experience Engine runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the experience store implementation and orchestrator with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional delegate to configure <see cref="ExperienceStoreOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddExperienceStore(
        this IServiceCollection services,
        Action<ExperienceStoreOptions>? configure = null)
    {
        var opts = new ExperienceStoreOptions();
        configure?.Invoke(opts);

        services.Configure<ExperienceStoreOptions>(o =>
        {
            o.StoreType = opts.StoreType;
            o.QdrantUrl = opts.QdrantUrl;
            o.FileDirectoryPath = opts.FileDirectoryPath;
            o.VectorSize = opts.VectorSize;
            o.Budget = opts.Budget;
        });

        if (opts.StoreType == ExperienceStoreType.Qdrant)
        {
            services.AddSingleton<QdrantClient>(_ =>
            {
                // QdrantClient constructor takes host + port, not a full URL.
                var uri = new Uri(opts.QdrantUrl);
                bool https = string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
                int port = uri.IsDefaultPort ? (https ? 443 : 6334) : uri.Port;
                return new QdrantClient(uri.Host, port, https);
            });
            services.AddSingleton<IQdrantClientWrapper, QdrantClientWrapper>();
            services.AddSingleton<IExperienceStore, QdrantExperienceStore>();
        }
        else
        {
            services.AddSingleton<IExperienceStore, FileExperienceStore>();
        }

        services.AddSingleton<ExperienceStoreOrchestrator>();

        return services;
    }
}
