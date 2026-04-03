using Asp.Versioning;

namespace Muonroi.AspNetCore.Extensions;

/// <summary>
/// Extensions for configuring common API services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds API versioning, Swagger and health checks.
    /// </summary>
    public static IServiceCollection AddBaseApi(this IServiceCollection services)
    {
        _ = services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = new ApiVersion(1, 0);
        });

        _ = services.AddEndpointsApiExplorer();
        _ = services.AddSwaggerGen();
        _ = services.AddHealthChecks();
        return services;
    }
}
