using Microsoft.EntityFrameworkCore;
using TestProject.Service.Core.Infrastructure;
using TestProject.Service.Core.Infrastructure.EntityConfigurations;

namespace TestProject.Service.Sites.Alpha;

/// <summary>
/// Alpha site EF Core DbContext — allows longer Name values (300 chars vs base 200).
/// Override OnModelCreating for Alpha-specific schema differences.
/// </summary>
public sealed class AlphaOrderContext : ContextBase
{
    public AlphaOrderContext(DbContextOptions<AlphaOrderContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Alpha allows longer names
        modelBuilder.Entity<OrderDetailEntity>().Property(e => e.Name).HasMaxLength(300);
    }
}
