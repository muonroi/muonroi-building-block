namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MRolePermissions")]
public sealed class MRolePermission : MEntity
{
    [Required] public Guid RoleId { get; set; }

    [Required] public Guid PermissionId { get; set; }
}
