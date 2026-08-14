namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Generic DbContext base for schema-divergent multi-tenancy.
/// Consumer defines concrete entity types via type parameters or DbSet properties.
/// Has ZERO hardcoded DbSets — consumer defines all of them.
///
/// Core services operate on DbContext.Set&lt;TEntity&gt;() without knowing concrete DbSet property names.
///
/// Usage:
/// <code>
/// public class MySiteContext : MDbContextBase&lt;MySiteContext&gt;
/// {
///     public DbSet&lt;MyEntity&gt; MyEntities =&gt; Set&lt;MyEntity&gt;();
///     protected override void ConfigureSiteSpecific(ModelBuilder b) { ... }
/// }
/// </code>
/// </summary>
public abstract class MDbContextBase<TContext> : DbContext
    where TContext : DbContext
{
    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    protected MDbContextBase(DbContextOptions<TContext> options) : base(options)
    {
    }

    /// <summary>
    /// Parameterless constructor for design-time tooling (EF migrations).
    /// </summary>
    protected MDbContextBase()
    {
    }

    /// <summary>
    /// Override SaveChangesAsync to apply audit rules before persisting.
    /// Automatically sets CreatedDate/UpdatedDate on IAuditable entities.
    /// Consumer can override ApplyAuditRules for custom audit (e.g., WorkContext-based user tracking).
    /// </summary>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Override SaveChangesAsync (convenience overload) to apply audit rules.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Apply audit timestamps to IAuditable entities.
    /// Default: sets CreatedDate on Added, UpdatedDate on Modified.
    /// Override for custom audit (e.g., set CreatedBy/UpdatedBy from WorkContext).
    /// </summary>
    protected virtual void ApplyAuditRules()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate ??= now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedDate = now;
                    break;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureSharedConventions(modelBuilder);
        ConfigureTenantFilters(modelBuilder);
        ConfigureSiteSpecific(modelBuilder);
    }

    /// <summary>
    /// Apply ecosystem conventions shared by all sites.
    /// Default: applies configurations from the concrete context's assembly.
    /// Override to add custom conventions (e.g., all string columns max 500, snake_case naming).
    /// </summary>
    protected virtual void ConfigureSharedConventions(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    /// <summary>
    /// Apply tenant query filters for ITenantScoped entities.
    /// Default: no-op. Override if using shared-schema multi-tenancy
    /// to add global query filters like:
    ///   modelBuilder.Entity&lt;T&gt;().HasQueryFilter(e =&gt; e.TenantId == currentTenantId)
    /// </summary>
    protected virtual void ConfigureTenantFilters(ModelBuilder modelBuilder)
    {
    }

    /// <summary>
    /// Site-specific model configuration. MUST override in site project.
    /// Add site-specific entity configurations, table mappings, relationships.
    /// </summary>
    protected abstract void ConfigureSiteSpecific(ModelBuilder modelBuilder);
}
