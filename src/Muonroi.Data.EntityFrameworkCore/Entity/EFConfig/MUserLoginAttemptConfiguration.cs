namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class MUserLoginAttemptConfiguration : IEntityTypeConfiguration<MUserLoginAttempt>
{
    public void Configure(EntityTypeBuilder<MUserLoginAttempt> builder)
    {
        builder.HasIndex(b => b.UserNameOrEmailAddress)
            .HasDatabaseName("IX_MUserLoginAttempt_UserNameOrEmailAddress").IsUnique(false);
    }
}
