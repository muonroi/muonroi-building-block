namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Configuration options for <see cref="SiteProfileDapperExtensions.AddSiteDapperInfrastructure"/>.
/// Consumer declares HOW to resolve connection strings for Dapper-based per-site data access.
/// Supports separate write and read replica connection strings.
///
/// <example>
/// <code>
/// services.AddSiteDapperInfrastructure(o =>
/// {
///     o.WriteConnectionString = sp => sp.GetRequiredService&lt;IWorkContextAccessor&gt;()
///         .WorkContext?.ConnectionString ?? throw new InvalidOperationException("No site");
///     o.ReadConnectionString = sp => sp.GetRequiredService&lt;IWorkContextAccessor&gt;()
///         .WorkContext?.ReadOnlyConnectionString;
///     o.ConnectionStringTransform = cs => Cryptography.Decrypt(secretKey, cs);
/// });
/// </code>
/// </example>
/// </summary>
public sealed class SiteDapperInfrastructureOptions
{
    /// <summary>
    /// Resolves the write (primary) connection string for the current site.
    /// Called once per scope (per HTTP request).
    /// REQUIRED.
    /// </summary>
    public Func<IServiceProvider, string>? WriteConnectionString { get; set; }

    /// <summary>
    /// Resolves the read replica connection string for the current site.
    /// When null, the write connection string is used for reads as well.
    /// Called once per scope (per HTTP request).
    /// </summary>
    public Func<IServiceProvider, string?>? ReadConnectionString { get; set; }

    /// <summary>
    /// Optional transform applied to connection strings before use.
    /// Common use case: decrypting encrypted connection strings.
    /// When null, raw connection strings are used as-is.
    /// </summary>
    public Func<string, string>? ConnectionStringTransform { get; set; }
}
