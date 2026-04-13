using Muonroi.Core.Abstractions.Models.Common;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class UpdateRoleRequestModelTests
{
    [Fact]
    public void Id_Getter_Returns_Value_Or_Default()
    {
        Guid id = Guid.NewGuid();
        UpdateRoleRequestModel model = new()
        {
            Name = "n",
            DisplayName = "d",
            Id = id
        };
        Assert.Equal(id, model.Id);

        UpdateRoleRequestModel model2 = new() { Name = "n", DisplayName = "d" };
        Assert.Equal(Guid.Empty, model2.Id);
    }

    [Fact]
    public void Name_Getter_Returns_Value_Or_Null()
    {
        UpdateRoleRequestModel model = new() { Name = "n", DisplayName = "d" };
        Assert.Equal("n", model.Name);
        model.Name = null!;
        Assert.Null(model.Name);
    }

    [Fact]
    public void DisplayName_Getter_Returns_Value_Or_Null()
    {
        UpdateRoleRequestModel model = new() { Name = "n", DisplayName = "d" };
        Assert.Equal("d", model.DisplayName);
        model.DisplayName = null!;
        Assert.Null(model.DisplayName);
    }

    [Fact]
    public void IsStatic_Getter_Returns_Value_Or_Default()
    {
        UpdateRoleRequestModel model = new()
        {
            Name = "n",
            DisplayName = "d",
            IsStatic = true
        };
        Assert.True(model.IsStatic);
        UpdateRoleRequestModel model2 = new() { Name = "n", DisplayName = "d" };
        Assert.False(model2.IsStatic);
    }

    [Fact]
    public void IsDefault_Getter_Returns_Value_Or_Default()
    {
        UpdateRoleRequestModel model = new()
        {
            Name = "n",
            DisplayName = "d",
            IsDefault = true
        };
        Assert.True(model.IsDefault);
        UpdateRoleRequestModel model2 = new() { Name = "n", DisplayName = "d" };
        Assert.False(model2.IsDefault);
    }
}
