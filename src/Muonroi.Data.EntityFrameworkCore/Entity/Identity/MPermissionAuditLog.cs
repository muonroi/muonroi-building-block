namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Stores audit information about permission changes.
/// </summary>
[Table("MPermissionAuditLogs")]
public class MPermissionAuditLog : MEntity
{
    /// <summary>Gets or sets the role identifier affected by the change.</summary>
    [Required]
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the permission identifier affected by the change.</summary>
    [Required]
    public Guid PermissionId { get; set; }

    /// <summary>Gets or sets the audit action name.</summary>
    [StringLength(64)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the user that performed the change, if known.</summary>
    public Guid? PerformedBy { get; set; }
}
