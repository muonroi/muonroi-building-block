namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
/// <summary>
/// Configures the <see cref="MPermissionGroup"/> entity model.
/// </summary>
public class MPermissionGroupConfiguration : IEntityTypeConfiguration<MPermissionGroup>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MPermissionGroup> builder)
    {
        builder.HasIndex(b => b.Name)
            .HasDatabaseName("IX_MPermissionGroups_Name")
            .IsUnique();
    }
}
