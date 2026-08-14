using Muonroi.Core.Abstractions.Interfaces;

namespace Quickstart.EntityFrameworkCore.Configuration.Api;

public enum AppPermission
{
    None
}

public class CustomerEntity : IAggregateRoot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CustomerConfiguration : MEntityConfigurationBase<CustomerEntity>
{
    protected override void ConfigureTable(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
    }

    protected override void ConfigureCoreColumns(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(255);
    }
    
    protected override void ConfigureCoreIndexes(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.HasIndex(x => x.Email).IsUnique();
    }
}

public class AppDbContext : MDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerEntity> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // This discovers CustomerConfiguration because it implements IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
