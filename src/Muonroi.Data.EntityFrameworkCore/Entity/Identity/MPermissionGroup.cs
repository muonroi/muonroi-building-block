namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MPermissionGroups")]
public class MPermissionGroup : MEntity
{
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(MaxNameLength)]
    public string DisplayName { get; set; } = string.Empty;
}
