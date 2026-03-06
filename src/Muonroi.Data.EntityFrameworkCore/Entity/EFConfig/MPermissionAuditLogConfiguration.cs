namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
public class MPermissionAuditLogConfiguration : IEntityTypeConfiguration<MPermissionAuditLog>
{
    public void Configure(EntityTypeBuilder<MPermissionAuditLog> builder)
    {
        builder.HasIndex(b => b.RoleId)
            .HasDatabaseName("IX_MPermissionAuditLogs_RoleId");
    }
}
