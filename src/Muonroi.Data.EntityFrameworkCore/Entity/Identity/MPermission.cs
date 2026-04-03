namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Represents a permission entity in the system.
/// </summary>
[Table("MPermissions")]
public sealed class MPermission : MEntity
{
    /// <summary>
    /// The maximum length allowed for the permission type.
    /// </summary>
    public const int MaxTypeLength = 32;
    /// <summary>
    /// The maximum length allowed for the UI key.
    /// </summary>
    public const int MaxUiKeyLength = 64;
    /// <summary>
    /// The maximum length allowed for the parent UI key.
    /// </summary>
    public const int MaxParentUiKeyLength = 64;
    /// <summary>
    /// The maximum length allowed for the label.
    /// </summary>
    public const int MaxLabelLength = 128;
    /// <summary>
    /// The maximum length allowed for the icon.
    /// </summary>
    public const int MaxIconLength = 128;
    /// <summary>
    /// The maximum length allowed for the description.
    /// </summary>
    public const int MaxDescriptionLength = 512;

    /// <summary>
    /// Gets or sets the name of the permission.
    /// </summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the permission is granted.
    /// </summary>
    public bool IsGranted { get; set; }

    /// <summary>
    /// Gets or sets the discriminator for the permission.
    /// </summary>
    [StringLength(255)] public string Discriminator { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the permission.
    /// </summary>
    [StringLength(MaxTypeLength)] public PermissionType Type { get; set; }

    /// <summary>
    /// Gets or sets the UI key for the permission.
    /// </summary>
    [Required]
    [StringLength(MaxUiKeyLength)]
    public string UiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent UI key for the permission.
    /// </summary>
    [StringLength(MaxParentUiKeyLength)] public string? ParentUiKey { get; set; }

    /// <summary>
    /// Gets or sets the label for the permission.
    /// </summary>
    [StringLength(MaxLabelLength)] public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the icon for the permission.
    /// </summary>
    [StringLength(MaxIconLength)] public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the display order of the permission.
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// Gets or sets the description of the permission.
    /// </summary>
    [StringLength(MaxDescriptionLength)] public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the parent permission ID.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the parent permission.
    /// </summary>
    public MPermission? Parent { get; set; }

    /// <summary>
    /// Gets or sets the child permissions.
    /// </summary>
    public ICollection<MPermission>? Children { get; set; }

    /// <summary>
    /// Gets or sets the permission group ID.
    /// </summary>
    public Guid? PermissionGroupId { get; set; }

    /// <summary>
    /// Gets or sets the permission group.
    /// </summary>
    public MPermissionGroup? PermissionGroup { get; set; }
}
