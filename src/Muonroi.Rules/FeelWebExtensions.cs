using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Rules.Contributors;

namespace Muonroi.Rules;

public static class FeelWebExtensions
{
    public static IServiceCollection AddFeelWeb(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, FeelPlaygroundManifestContributor>());
        services.AddControllers().AddApplicationPart(typeof(Controllers.FeelController).Assembly);
        return services;
    }
}
