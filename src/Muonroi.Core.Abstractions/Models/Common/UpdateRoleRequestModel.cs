namespace Muonroi.Core.Abstractions.Models.Common;

public class UpdateRoleRequestModel
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public bool IsStatic { get; set; }
    public bool IsDefault { get; set; }
}
