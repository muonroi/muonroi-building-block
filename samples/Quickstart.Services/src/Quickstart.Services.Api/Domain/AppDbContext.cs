using Microsoft.EntityFrameworkCore;

namespace Quickstart.Services.Api.Domain;

/// <summary>
/// EF Core context. MServiceBase resolves tables via Set&lt;TEntity&gt;(), so a single
/// DbSet is enough for the generic CRUD logic to work.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
