namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents a request to assign a permission to a role. </summary>
public class AssignPermissionRequestModel
{
    /// <summary> Gets or sets the ID of the role. </summary>
    public Guid RoleId { get; set; }

    /// <summary> Gets or sets the ID of the permission. </summary>
    public Guid PermissionId { get; set; }
}
