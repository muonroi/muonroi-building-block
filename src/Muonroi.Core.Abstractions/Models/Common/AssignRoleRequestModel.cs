namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents a request to assign a role to a user. </summary>
public class AssignRoleRequestModel
{
    /// <summary> Gets or sets the ID of the role. </summary>
    public Guid RoleId { get; set; }

    /// <summary> Gets or sets the ID of the user. </summary>
    public Guid UserId { get; set; }
}
