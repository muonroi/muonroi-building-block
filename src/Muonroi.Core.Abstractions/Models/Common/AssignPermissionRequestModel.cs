namespace Muonroi.Core.Abstractions.Models.Common;

public class AssignPermissionRequestModel
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}
