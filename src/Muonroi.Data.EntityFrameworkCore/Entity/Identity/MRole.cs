namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MRoles")]
public sealed class MRole : MEntity
{
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(MaxNameLength)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(MaxNameLength)]
    public string NormalizedName { get; set; } = string.Empty;

    public bool IsStatic { get; set; }

    public bool IsDefault { get; set; }

    [StringLength(128)] public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
}
