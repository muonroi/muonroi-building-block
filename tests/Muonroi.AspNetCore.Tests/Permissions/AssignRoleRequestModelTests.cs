using Muonroi.Core.Abstractions.Models.Common;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class AssignRoleRequestModelTests
{
    [Fact]
    public void RoleId_Returns_Value_Or_Default()
    {
        AssignRoleRequestModel model = new();
        Assert.Equal(Guid.Empty, model.RoleId);
        Guid id = Guid.NewGuid();
        model.RoleId = id;
        Assert.Equal(id, model.RoleId);
    }

    [Fact]
    public void UserId_Returns_Value_Or_Default()
    {
        AssignRoleRequestModel model = new();
        Assert.Equal(Guid.Empty, model.UserId);
        Guid id = Guid.NewGuid();
        model.UserId = id;
        Assert.Equal(id, model.UserId);
    }
}
