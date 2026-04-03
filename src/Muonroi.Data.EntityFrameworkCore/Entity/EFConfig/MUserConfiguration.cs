namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Configures the <see cref="MUser"/> entity model.
/// </summary>
public class MUserConfiguration : IEntityTypeConfiguration<MUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MUser> builder)
    {
        builder.HasIndex(b => b.UserName)
            .HasDatabaseName("IX_MUser_UserName").IsUnique();
    }
}
