namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary>
/// Represents the request model for updating an existing role.
/// </summary>
public class UpdateRoleRequestModel
{
    /// <summary>
    /// Gets or sets the role's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the display name of the role.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the role is static.
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the role is a default role.
    /// </summary>
    public bool IsDefault { get; set; }
}
