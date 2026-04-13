using Microsoft.EntityFrameworkCore;
using TestProject.Service.Core.Infrastructure;
using TestProject.Service.Core.Infrastructure.EntityConfigurations;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// Bravo site EF Core DbContext — adds ContainerNo index for faster lookups.
/// Override OnModelCreating for Bravo-specific schema differences.
/// </summary>
public sealed class BravoOrderContext(DbContextOptions<BravoOrderContext> options) : ContextBase(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Bravo adds index on ContainerNo for faster lookups
        modelBuilder.Entity<OrderDetailEntity>().HasIndex(e => e.ContainerNo);
    }
}
