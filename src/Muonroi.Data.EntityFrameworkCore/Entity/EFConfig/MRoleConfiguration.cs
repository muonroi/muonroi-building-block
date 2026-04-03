namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Configures the <see cref="MRole"/> entity model.
/// </summary>
public class MRoleConfiguration : IEntityTypeConfiguration<MRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MRole> builder)
    {
        builder.HasIndex(b => b.Name)
            .HasDatabaseName("IX_MRoles_Name")
            .IsUnique();
    }
}
