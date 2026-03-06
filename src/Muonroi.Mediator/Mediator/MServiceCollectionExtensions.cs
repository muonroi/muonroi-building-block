using Microsoft.Extensions.DependencyInjection;
using Muonroi.Mediator.Mediator.Interfaces;
using System.Reflection;

namespace Muonroi.Mediator.Mediator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[]? assemblies)
    {
        services.AddSingleton<IMediator, MMediator>();
        services.AddTransient<ServiceFactory>(sp => sp.GetService);
        if (assemblies is not { Length: > 0 }) return services;
        foreach (Assembly assembly in assemblies)
        foreach (TypeInfo type in assembly.DefinedTypes)
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            foreach (Type face in type.ImplementedInterfaces)
            {
                if (!face.IsGenericType)
                    continue;

                    Type def = face.GetGenericTypeDefinition();
                if (def != typeof(IRequestHandler<,>) &&
                    def != typeof(INotificationHandler<>) &&
                    def != typeof(IStreamRequestHandler<,>) &&
                    def != typeof(IPipelineBehavior<,>)) continue;
                services.AddTransient(type.IsGenericTypeDefinition ? def : face, type.AsType());
            }
        }

        return services;
    }
}
