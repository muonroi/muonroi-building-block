namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Configures the <see cref="MUserRole"/> entity model.
/// </summary>
public class MUserRoleConfiguration : IEntityTypeConfiguration<MUserRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MUserRole> builder)
    {
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.UserId, x.RoleId });
    }
}
