using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.CEP;

public static class CepWebExtensions
{
    public static IServiceCollection AddCepWeb(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, Contributors.CepManifestContributor>());
        services.AddControllers().AddApplicationPart(typeof(Controllers.CepController).Assembly);
        return services;
    }
}
