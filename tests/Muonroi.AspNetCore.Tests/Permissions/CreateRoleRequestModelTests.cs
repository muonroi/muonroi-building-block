using Muonroi.Core.Abstractions.Models.Common;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class CreateRoleRequestModelTests
{
    [Fact]
    public void Name_Returns_Value_Or_Null()
    {
        CreateRoleRequestModel model = new() { Name = "role", DisplayName = "Role" };
        Assert.Equal("role", model.Name);
        model.Name = null!;
        Assert.Null(model.Name);
    }

    [Fact]
    public void DisplayName_Returns_Value_Or_Null()
    {
        CreateRoleRequestModel model = new() { Name = "r", DisplayName = "disp" };
        Assert.Equal("disp", model.DisplayName);
        model.DisplayName = null!;
        Assert.Null(model.DisplayName);
    }

    [Fact]
    public void IsStatic_Returns_Value()
    {
        CreateRoleRequestModel model = new() { Name = "r", DisplayName = "d" };
        Assert.False(model.IsStatic);
        model.IsStatic = true;
        Assert.True(model.IsStatic);
    }

    [Fact]
    public void IsDefault_Returns_Value()
    {
        CreateRoleRequestModel model = new() { Name = "r", DisplayName = "d" };
        Assert.False(model.IsDefault);
        model.IsDefault = true;
        Assert.True(model.IsDefault);
    }
}
