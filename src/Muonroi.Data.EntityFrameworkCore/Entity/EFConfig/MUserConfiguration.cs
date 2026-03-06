namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class MUserConfiguration : IEntityTypeConfiguration<MUser>
{
    public void Configure(EntityTypeBuilder<MUser> builder)
    {
        builder.HasIndex(b => b.UserName)
            .HasDatabaseName("IX_MUser_UserName").IsUnique();
    }
}
