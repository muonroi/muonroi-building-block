using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TestProject.Service.Core.Infrastructure.EntityConfigurations;

/// <summary>
/// Base entity configuration for the OrderDetail entity.
/// Site-specific contexts can override via their own IEntityTypeConfiguration.
/// </summary>
public class OrderDetailConfigBase : IEntityTypeConfiguration<OrderDetailEntity>
{
    public virtual void Configure(EntityTypeBuilder<OrderDetailEntity> builder)
    {
        builder.ToTable("order_details");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.ContainerNo).HasMaxLength(100);
    }
}

/// <summary>
/// Example entity — replace with your actual domain entity.
/// </summary>
public class OrderDetailEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ContainerNo { get; set; }
}
