namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Joins roles to their permissions.
/// </summary>
[Table("MRolePermissions")]
public sealed class MRolePermission : MEntity
{
    /// <summary>Gets or sets the role identifier.</summary>
    [Required] public Guid RoleId { get; set; }

    /// <summary>Gets or sets the permission identifier.</summary>
    [Required] public Guid PermissionId { get; set; }
}
