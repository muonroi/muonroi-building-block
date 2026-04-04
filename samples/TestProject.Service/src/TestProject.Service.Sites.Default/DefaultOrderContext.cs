using Microsoft.EntityFrameworkCore;
using TestProject.Service.Core.Infrastructure;

namespace TestProject.Service.Sites.Default;

/// <summary>
/// Default site EF Core DbContext — inherits shared configuration.
/// Override OnModelCreating for Default-specific schema differences.
/// </summary>
public sealed class DefaultOrderContext(DbContextOptions<DefaultOrderContext> options) : ContextBase(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Default site uses base schema — no overrides.
    }
}
