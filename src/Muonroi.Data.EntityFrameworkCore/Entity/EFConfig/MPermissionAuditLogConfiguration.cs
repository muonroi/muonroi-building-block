namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
/// <summary>
/// Configures the <see cref="MPermissionAuditLog"/> entity model.
/// </summary>
public class MPermissionAuditLogConfiguration : IEntityTypeConfiguration<MPermissionAuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MPermissionAuditLog> builder)
    {
        builder.HasIndex(b => b.RoleId)
            .HasDatabaseName("IX_MPermissionAuditLogs_RoleId");
    }
}
