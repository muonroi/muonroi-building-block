namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Maps users to roles.
/// </summary>
[Table("MUserRoles")]
public sealed class MUserRole : MEntity
{
    /// <summary>Gets or sets the user identifier.</summary>
    [Required] public Guid UserId { get; set; }

    /// <summary>Gets or sets the role identifier.</summary>
    [Required] public Guid RoleId { get; set; }
}
