using Microsoft.EntityFrameworkCore;

namespace MuonroiService.Core.Persistence;

/// <summary>
/// Base DbContext for all site-specific contexts.
/// Contains shared entity configurations. Each site inherits this
/// and can override OnModelCreating for site-specific schema differences.
/// </summary>
public abstract class ContextBase : DbContext
{
    protected ContextBase(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply shared entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContextBase).Assembly);
    }
}
