namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents a request to create a new role. </summary>
public class CreateRoleRequestModel
{
    /// <summary> Gets or sets the name of the role. </summary>
    public required string Name { get; set; }

    /// <summary> Gets or sets the display name of the role. </summary>
    public required string DisplayName { get; set; }

    /// <summary> Gets or sets a value indicating whether the role is static. </summary>
    public bool IsStatic { get; set; }

    /// <summary> Gets or sets a value indicating whether the role is default. </summary>
    public bool IsDefault { get; set; }
}
