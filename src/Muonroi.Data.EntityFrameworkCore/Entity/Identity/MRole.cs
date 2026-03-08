namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Represents a role entity in the system.
/// </summary>
[Table("MRoles")]
public sealed class MRole : MEntity
{
    /// <summary>
    /// Gets or sets the unique name of the role.
    /// </summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the role.
    /// </summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the normalized name of the role.
    /// </summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the role is static (cannot be deleted).
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the role is a default role for new users.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the concurrency stamp for optimistic concurrency.
    /// </summary>
    [StringLength(128)] public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
}
