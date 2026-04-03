using Microsoft.OpenApi.Models;
using Microsoft.Net.Http.Headers;
using Muonroi.AspNetCore.OpenApi;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Helpers;
using Muonroi.Mapper.Mapper;
using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Interfaces;
using System.Text.Json.Serialization;

namespace Muonroi.AspNetCore.Extensions;

/// <inheritdoc />
public static class ApplicationExtensions
{
/// <inheritdoc />
    public static IServiceCollection AddApplication(this IServiceCollection services, params Assembly[]? assemblies)
    {
        _ = services.AddHttpContextAccessor();

        if (assemblies?.Length > 0)
        {
            _ = services.AddMediator(assemblies);
        }

        _ = services.ConfigureMapper(assemblies ?? []);

        _ = services.AddProblemDetails();

        services.AddConfigureHttpJson();

        _ = services.AddSingleton<IMDateTimeService, MDateTimeService>();
        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.TryAddSingleton<IContextResolver, NullContextResolver>();
        services.TryAddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>();

        Assembly? firstAssembly = assemblies?.FirstOrDefault();
        if (firstAssembly != null)
        {
            _ = services.AddControllerConfiguration(firstAssembly);
        }

        return services;
    }

/// <inheritdoc />
    public static IServiceCollection AddConfigureHttpJson(this IServiceCollection services)
    {
        MGuard.NotNull(services);
        _ = services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.WriteIndented = false;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        _ = services.Configure<JsonSerializerOptions>(opts =>
        {
            opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.WriteIndented = false;
            opts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            opts.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        return services;
    }

/// <inheritdoc />
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[]? assemblies)
    {
        services.AddSingleton<IMediator, MMediator>();
        services.AddTransient<ServiceFactory>(sp => sp.GetService);
        if (assemblies is not { Length: > 0 })
        {
            return services;
        }

        foreach (Assembly assembly in assemblies)
        {
            foreach (TypeInfo type in assembly.DefinedTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                foreach (Type face in type.ImplementedInterfaces)
                {
                    if (!face.IsGenericType)
                    {
                        continue;
                    }

                    Type def = face.GetGenericTypeDefinition();
                    if (def != typeof(IRequestHandler<,>) &&
                        def != typeof(INotificationHandler<>) &&
                        def != typeof(IStreamRequestHandler<,>) &&
                        def != typeof(IPipelineBehavior<,>))
                    {
                        continue;
                    }

                    services.AddTransient(type.IsGenericTypeDefinition ? def : face, type.AsType());
                }
            }
        }

        return services;
        }

/// <inheritdoc />
        public static WebApplication AddLocalization(this WebApplication app, Assembly assembly)
        {
        ResourceSetting resourceSetting = app.Services.GetRequiredService<ResourceSetting>();
        MHelpers.Initialize(resourceSetting, assembly);

        return app;
        }

/// <inheritdoc />
        public static IServiceCollection SwaggerConfig(this IServiceCollection services, string serviceName)
    {
        _ = services.AddSwaggerGen(config =>
        {
            config.SwaggerDoc("v1", new OpenApiInfo { Title = serviceName, Version = "v1" });

            config.OperationFilter<MErrorResponseFilter>(); // D-11: Standardized error documentation

            config.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = HeaderNames.Authorization,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme."
            });

            config.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    []
                }
            });
        });

        return services;
    }

/// <inheritdoc />
    public static WebApplicationBuilder AddAppConfiguration(this WebApplicationBuilder builder)
    {
        _ = builder.Configuration
            .AddJsonFile("appsettings." + builder.Environment.EnvironmentName + ".json", true, true)
            .AddEnvironmentVariables();
        return builder;
    }
}
