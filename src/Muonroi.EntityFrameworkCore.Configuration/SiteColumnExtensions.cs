namespace Muonroi.EntityFrameworkCore.Configuration;

/// <summary>
/// Extension methods for applying [SiteColumn] attribute-based column overrides to EF Core model builder.
/// </summary>
public static class SiteColumnExtensions
{
    /// <summary>
    /// Reads <see cref="SiteColumnAttribute"/> attributes from <typeparamref name="TEntity"/> properties
    /// and applies them to the EF Core model builder.
    /// Properties without the attribute are mapped to UPPER_SNAKE_CASE by convention.
    /// Call inside <c>ConfigureSiteColumns(builder)</c> or <c>ConfigureSiteSpecific()</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure.</typeparam>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="siteId">
    /// The site identifier. Passed for context (e.g., logging, conditional logic in future extensions).
    /// The attribute itself is not site-keyed — it is placed on the entity class used by a specific
    /// site configuration, providing implicit site scoping.
    /// </param>
    public static void ApplySiteColumnOverrides<TEntity>(
        this ModelBuilder modelBuilder,
        string siteId)
        where TEntity : class
    {
        var entityType = modelBuilder.Entity<TEntity>();
        foreach (var prop in typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
            if (attr == null)
            {
                // Convention: PascalCase → UPPER_SNAKE_CASE when no attribute is present
                entityType.Property(prop.Name).HasColumnName(ToUpperSnakeCase(prop.Name));
                continue;
            }

            if (attr.Ignore)
            {
                entityType.Ignore(prop.Name);
                continue;
            }

            var propBuilder = entityType.Property(prop.Name);

            // Name override or UPPER_SNAKE_CASE convention
            propBuilder.HasColumnName(attr.Name ?? ToUpperSnakeCase(prop.Name));

            if (attr.MaxLength > 0)
            {
                propBuilder.HasMaxLength(attr.MaxLength);
            }

            if (attr.IsRequired)
            {
                propBuilder.IsRequired();
            }

            if (attr.DefaultValue != null)
            {
                propBuilder.HasDefaultValue(attr.DefaultValue);
            }

            if (attr.HasColumnType != null)
            {
                propBuilder.HasColumnType(attr.HasColumnType);
            }
        }
    }

    /// <summary>
    /// Converts a PascalCase property name to UPPER_SNAKE_CASE for use as a database column name.
    /// Delegates to <see cref="ColumnNamingConvention.ToUpperSnakeCase"/> — the single source of truth
    /// shared with the Dapper/ISiteColumnMap layer.
    /// </summary>
    internal static string ToUpperSnakeCase(string pascalCase)
        => ColumnNamingConvention.ToUpperSnakeCase(pascalCase);
}
