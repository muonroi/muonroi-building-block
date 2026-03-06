namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class MUserRoleConfiguration : IEntityTypeConfiguration<MUserRole>
{
    public void Configure(EntityTypeBuilder<MUserRole> builder)
    {
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.UserId, x.RoleId });
    }
}
