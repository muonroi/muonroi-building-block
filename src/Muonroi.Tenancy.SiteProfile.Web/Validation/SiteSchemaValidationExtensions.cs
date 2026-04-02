using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Muonroi.Tenancy.SiteProfile.Web.Validation;

/// <summary>
/// DI extension methods for <see cref="SiteSchemaValidator"/>.
/// </summary>
public static class SiteSchemaValidationExtensions
{
    /// <summary>
    /// Registers <see cref="SiteSchemaValidator"/> as an <see cref="IHostedService"/> that validates
    /// EF Core model columns against the actual database schema at application startup.
    ///
    /// This is opt-in — call this method to enable schema validation. If not called, no validation runs.
    /// Validation is additive and backward-compatible (per D-28) — it does not change any existing behavior.
    ///
    /// <example>
    /// <code>
    /// // Warn on mismatch (default — suitable for development/staging):
    /// services.AddSiteSchemaValidation();
    ///
    /// // Fail on missing column (strict — suitable for production):
    /// services.AddSiteSchemaValidation(o =>
    /// {
    ///     o.Severity = SchemaValidationSeverity.FailOnMissing;
    ///     o.SchemaName = "public"; // PostgreSQL, or "dbo" for SQL Server
    /// });
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to configure validation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteSchemaValidation(
        this IServiceCollection services,
        Action<SiteSchemaValidationOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<SiteSchemaValidationOptions>();

        services.AddHostedService<SiteSchemaValidator>();
        return services;
    }
}
