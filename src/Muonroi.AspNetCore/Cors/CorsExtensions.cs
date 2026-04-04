namespace Muonroi.AspNetCore.Cors;

/// <inheritdoc />
public static class CorsExtensions
{
/// <inheritdoc />
    public static IServiceCollection AddCors(this IServiceCollection services, IConfiguration configuration,
        string domainName = "MAllowDomains")
    {
        string[] origins = configuration[domainName]?.Trim().Split(",") ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(domainName, policy =>
            {
                policy.WithOrigins(origins)
                    .WithHeaders(
                        "Content-Type",
                        "Authorization",
                        "Accept",
                        "Accept-Language",
                        "X-Tenant-Id",
                        "X-Request-Id",
                        "X-Correlation-Id")
                    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                    .AllowCredentials()
                    .WithExposedHeaders("X-Correlation-Id", "X-Request-Id");
            });
        });

        return services;
    }
}
