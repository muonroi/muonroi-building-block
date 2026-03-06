namespace Muonroi.AspNetCore.Cors;

public static class CorsExtensions
{
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
