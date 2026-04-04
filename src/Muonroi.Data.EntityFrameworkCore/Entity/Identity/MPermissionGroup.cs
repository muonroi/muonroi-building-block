namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Represents a logical group of permissions for UI or policy organization.
/// </summary>
[Table("MPermissionGroups")]
public class MPermissionGroup : MEntity
{
    /// <summary>Gets or sets the group name.</summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name shown to users.</summary>
    [StringLength(MaxNameLength)]
    public string DisplayName { get; set; } = string.Empty;
}
