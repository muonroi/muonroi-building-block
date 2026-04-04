namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents a request to create a new permission. </summary>
public class CreatePermissionRequestModel
{
    /// <summary> Gets or sets the name of the permission. </summary>
    public required string Name { get; set; }

    /// <summary> Gets or sets a value indicating whether the permission is granted. </summary>
    public bool IsGranted { get; set; } = true;

    /// <summary> Gets or sets the discriminator for the permission. </summary>
    public string Discriminator { get; set; } = "";
}
