namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MPermissions")]
public sealed class MPermission : MEntity
{
    public const int MaxTypeLength = 32;
    public const int MaxUiKeyLength = 64;
    public const int MaxParentUiKeyLength = 64;
    public const int MaxLabelLength = 128;
    public const int MaxIconLength = 128;
    public const int MaxDescriptionLength = 512;

    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public bool IsGranted { get; set; }

    [StringLength(255)] public string Discriminator { get; set; } = string.Empty;

    [StringLength(MaxTypeLength)] public PermissionType Type { get; set; }

    [Required]
    [StringLength(MaxUiKeyLength)]
    public string UiKey { get; set; } = string.Empty;

    [StringLength(MaxParentUiKeyLength)] public string? ParentUiKey { get; set; }

    [StringLength(MaxLabelLength)] public string? Label { get; set; }

    [StringLength(MaxIconLength)] public string? Icon { get; set; }

    public int? Order { get; set; }

    [StringLength(MaxDescriptionLength)] public string? Description { get; set; }

    public Guid? ParentId { get; set; }

    public MPermission? Parent { get; set; }

    public ICollection<MPermission>? Children { get; set; }

    public Guid? PermissionGroupId { get; set; }

    public MPermissionGroup? PermissionGroup { get; set; }
}
