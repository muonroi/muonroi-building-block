namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MPermissionAuditLogs")]
public class MPermissionAuditLog : MEntity
{
    [Required]
    public Guid RoleId { get; set; }

    [Required]
    public Guid PermissionId { get; set; }

    [StringLength(64)]
    public string Action { get; set; } = string.Empty;

    public Guid? PerformedBy { get; set; }
}
