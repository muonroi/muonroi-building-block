

namespace Muonroi.BuildingBlock.Test;

public class AssignPermissionRequestModelTests
{
    [Fact]
    public void RoleId_Returns_Set_Value()
    {
        AssignPermissionRequestModel model = new()
        {
            RoleId = Guid.NewGuid()
        };
        Assert.Equal(model.RoleId, model.RoleId);
    }

    [Fact]
    public void RoleId_Default_When_Not_Set()
    {
        AssignPermissionRequestModel model = new();
        Assert.Equal(Guid.Empty, model.RoleId);
    }

    [Fact]
    public void PermissionId_Returns_Set_Value()
    {
        AssignPermissionRequestModel model = new()
        {
            PermissionId = Guid.NewGuid()
        };
        Assert.Equal(model.PermissionId, model.PermissionId);
    }

    [Fact]
    public void PermissionId_Default_When_Not_Set()
    {
        AssignPermissionRequestModel model = new();
        Assert.Equal(Guid.Empty, model.PermissionId);
    }
}
