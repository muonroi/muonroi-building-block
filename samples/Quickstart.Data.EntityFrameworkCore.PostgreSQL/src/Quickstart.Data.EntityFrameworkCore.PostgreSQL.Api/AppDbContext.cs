namespace Quickstart.Data.EntityFrameworkCore.PostgreSQL.Api;

public enum AppPermission
{
    None,
    ViewSample
}

public class SampleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AppDbContext : MDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SampleEntity> Samples { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<SampleEntity>().HasKey(x => x.Id);
    }
}
