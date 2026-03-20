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
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
