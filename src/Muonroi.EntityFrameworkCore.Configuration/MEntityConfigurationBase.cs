using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Muonroi.EntityFrameworkCore.Configuration;

/// <summary>
/// Composable entity configuration base.
/// Core configures shared columns/indexes. Site extends with site-specific columns.
/// Implements IEntityTypeConfiguration so EF discovers it via ApplyConfigurationsFromAssembly.
/// </summary>
public abstract class MEntityConfigurationBase<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class
{
    /// <summary>
    /// EF Core entry point. Calls the template methods in order:
    /// ConfigureTable → ConfigureCoreColumns → ConfigureCoreIndexes → ConfigureSiteColumns → ConfigureSiteIndexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ConfigureTable(builder);
        ConfigureCoreColumns(builder);
        ConfigureCoreIndexes(builder);
        ConfigureSiteColumns(builder);
        ConfigureSiteIndexes(builder);
    }

    /// <summary>
    /// Configure table name, schema, and primary key.
    /// </summary>
    protected abstract void ConfigureTable(EntityTypeBuilder<TEntity> builder);

    /// <summary>
    /// Configure columns shared across ALL sites (70-80% of columns).
    /// Maps property names to database column names, sets max lengths, Unicode, etc.
    /// </summary>
    protected abstract void ConfigureCoreColumns(EntityTypeBuilder<TEntity> builder);

    /// <summary>
    /// Configure indexes shared across ALL sites.
    /// Default: no shared indexes. Override to add.
    /// </summary>
    protected virtual void ConfigureCoreIndexes(EntityTypeBuilder<TEntity> builder) { }

    /// <summary>
    /// Configure site-specific columns.
    /// Override in site project to add/remap columns that differ.
    /// Default: no-op (small sites that match core schema).
    /// Example: builder.Property(e => e.BookingNo).HasColumnName("BOOKING_NUMBER");
    /// </summary>
    protected virtual void ConfigureSiteColumns(EntityTypeBuilder<TEntity> builder) { }

    /// <summary>
    /// Configure site-specific indexes.
    /// Override in site project to add indexes on site-specific columns.
    /// Default: no-op.
    /// </summary>
    protected virtual void ConfigureSiteIndexes(EntityTypeBuilder<TEntity> builder) { }
}
