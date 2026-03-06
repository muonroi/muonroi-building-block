namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
public class MPermissionGroupConfiguration : IEntityTypeConfiguration<MPermissionGroup>
{
    public void Configure(EntityTypeBuilder<MPermissionGroup> builder)
    {
        builder.HasIndex(b => b.Name)
            .HasDatabaseName("IX_MPermissionGroups_Name")
            .IsUnique();
    }
}
