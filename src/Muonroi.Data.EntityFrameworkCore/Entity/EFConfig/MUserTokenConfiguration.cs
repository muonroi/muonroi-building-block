namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Configures the <see cref="MUserToken"/> entity model.
/// </summary>
public class MUserTokenConfiguration : IEntityTypeConfiguration<MUserToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MUserToken> builder)
    {
        builder.HasIndex(b => b.LoginProvider).HasDatabaseName("IX_MUserToken_LoginProvider").IsUnique(false);
    }
}
