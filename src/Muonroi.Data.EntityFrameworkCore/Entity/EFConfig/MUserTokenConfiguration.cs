namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class MUserTokenConfiguration : IEntityTypeConfiguration<MUserToken>
{
    public void Configure(EntityTypeBuilder<MUserToken> builder)
    {
        builder.HasIndex(b => b.LoginProvider).HasDatabaseName("IX_MUserToken_LoginProvider").IsUnique(false);
    }
}
