namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class MRoleConfiguration : IEntityTypeConfiguration<MRole>
{
    public void Configure(EntityTypeBuilder<MRole> builder)
    {
        builder.HasIndex(b => b.Name)
            .HasDatabaseName("IX_MRoles_Name")
            .IsUnique();
    }
}
