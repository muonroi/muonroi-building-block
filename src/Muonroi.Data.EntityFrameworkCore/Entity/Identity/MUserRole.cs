namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MUserRoles")]
public sealed class MUserRole : MEntity
{
    [Required] public Guid UserId { get; set; }

    [Required] public Guid RoleId { get; set; }
}
