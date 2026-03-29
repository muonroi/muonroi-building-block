namespace Muonroi.Tenancy.SiteProfile.Web.Validation;

/// <summary>
/// Options for configuring <see cref="SiteSchemaValidator"/> behavior.
/// Register via <c>services.AddSiteSchemaValidation(o => ...)</c>.
/// </summary>
public sealed class SiteSchemaValidationOptions
{
    /// <summary>
    /// Controls behavior on schema mismatch.
    /// Default: <see cref="SchemaValidationSeverity.WarnOnMismatch"/> — logs warnings, does not throw.
    /// Set to <see cref="SchemaValidationSeverity.FailOnMissing"/> for strict startup validation.
    /// </summary>
    public SchemaValidationSeverity Severity { get; set; } = SchemaValidationSeverity.WarnOnMismatch;

    /// <summary>
    /// The database schema name to query in INFORMATION_SCHEMA.
    /// Default: <c>"public"</c> (PostgreSQL convention).
    /// Use <c>"dbo"</c> for SQL Server.
    /// </summary>
    public string SchemaName { get; set; } = "public";
}

/// <summary>
/// Controls how <see cref="SiteSchemaValidator"/> reports EF model vs actual DB schema mismatches.
/// </summary>
public enum SchemaValidationSeverity
{
    /// <summary>
    /// Log a warning per missing or mismatched column. Does NOT stop the application.
    /// Suitable for staging/dev environments where drift is expected during migration.
    /// </summary>
    WarnOnMismatch = 0,

    /// <summary>
    /// Throw <see cref="InvalidOperationException"/> listing all missing columns.
    /// Suitable for production — prevents serving requests against a schema-drifted database.
    /// </summary>
    FailOnMissing = 1
}
