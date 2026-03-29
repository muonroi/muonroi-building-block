namespace Muonroi.EntityFrameworkCore.Configuration;

/// <summary>
/// Decorates an entity property to override EF Core column mapping for site-specific configurations.
/// Applied by <see cref="SiteColumnExtensions.ApplySiteColumnOverrides{TEntity}"/> during model building.
/// Properties without this attribute are mapped to UPPER_SNAKE_CASE by convention.
/// </summary>
/// <remarks>
/// Does NOT replace virtual methods for complex scenarios (computed columns, complex indexes).
/// For complex configuration, continue using ConfigureSiteColumns(builder) virtual override in
/// <see cref="MEntityConfigurationBase{TEntity}"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SiteColumnAttribute : Attribute
{
    /// <summary>
    /// Override the database column name. When null, UPPER_SNAKE_CASE convention applies.
    /// Example: Name = "CUSTOM_COLUMN_NAME" → HasColumnName("CUSTOM_COLUMN_NAME")
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Sets the maximum string length for the column. Use -1 (default) to not override.
    /// Example: MaxLength = 25 → HasMaxLength(25)
    /// </summary>
    public int MaxLength { get; set; } = -1;

    /// <summary>
    /// Forces the column to be required (NOT NULL). Default false = use EF convention.
    /// Example: IsRequired = true → IsRequired()
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Sets the default value for the column. When null, no default is applied.
    /// Example: DefaultValue = "N" → HasDefaultValue("N")
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Sets the database column type. When null, EF convention applies.
    /// Example: HasColumnType = "decimal(18,4)" → HasColumnType("decimal(18,4)")
    /// </summary>
    public string? HasColumnType { get; set; }

    /// <summary>
    /// When true, EF Core ignores this property entirely (not mapped to any column).
    /// Example: Ignore = true → Ignore(propertyName)
    /// </summary>
    public bool Ignore { get; set; }
}
