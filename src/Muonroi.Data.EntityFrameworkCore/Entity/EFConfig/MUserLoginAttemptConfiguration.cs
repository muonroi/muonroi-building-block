namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Configures the <see cref="MUserLoginAttempt"/> entity model.
/// </summary>
public class MUserLoginAttemptConfiguration : IEntityTypeConfiguration<MUserLoginAttempt>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MUserLoginAttempt> builder)
    {
        builder.HasIndex(b => b.UserNameOrEmailAddress)
            .HasDatabaseName("IX_MUserLoginAttempt_UserNameOrEmailAddress").IsUnique(false);
    }
}
